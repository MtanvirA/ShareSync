using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;

namespace ShareSync.DataImporter
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Historical Data Importer for MySQL...");
            
            string csvFilePath = @"E:\Projects\Oracle+WebProgramming\ShareSync\Dhaka Stock Exchange Historical Data (1999-2025)\DSE_Data.csv";
            string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MySqlConnection")
                ?? "Server=localhost;Port=3306;Database=sharesync;User=root;Password=YOUR_PASSWORD;CharSet=utf8mb4;";
            
            if (!File.Exists(csvFilePath))
            {
                Console.WriteLine($"File not found: {csvFilePath}");
                return;
            }
            
            var optionsBuilder = new DbContextOptionsBuilder<ShareSyncDbContext>();
            optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)));
            using var context = new ShareSyncDbContext(optionsBuilder.Options);
            
            // Ensure connection
            try
            {
                context.Database.OpenConnection();
                Console.WriteLine("Database connected successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database connection failed: {ex.Message}");
                return;
            }
            
            var companies = context.Companies.ToList();
            var companyMap = companies.ToDictionary(c => c.TickerSymbol.ToUpperInvariant(), c => c.CompanyId);
            
            Console.WriteLine($"Loaded {companyMap.Count} companies from DB.");
            
            int totalRows = 0;
            int exactDuplicates = 0;
            int conflictingDuplicates = 0;
            int invalidOhlc = 0;
            int zeroPrice = 0;
            int unmatchedCodes = 0;
            int matchedCodes = 0;
            
            // Fetch existing records for idempotency
            Console.WriteLine("Fetching existing records for idempotency check...");
            var existingRecords = context.CompanyPriceHistories
                .Select(h => new { h.CompanyId, h.TradingDate })
                .ToHashSet();
            
            // To track duplicates: Trading_Code + Date
            var seenRecords = new Dictionary<string, (decimal Open, decimal High, decimal Low, decimal Close, long? Volume)>();
            var validRecords = new List<CompanyPriceHistory>();
            int missingValueCount = 0;
            DateTime minDate = DateTime.MaxValue;
            DateTime maxDate = DateTime.MinValue;
            
            var matchedList = new HashSet<string>();
            var unmatchedList = new HashSet<string>();
            var ignoredList = new HashSet<string>();
            
            var batchId = Guid.NewGuid().ToString();
            
            using (var reader = new StreamReader(csvFilePath))
            {
                string? header = reader.ReadLine();
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    totalRows++;
                    if (totalRows % 100000 == 0) Console.WriteLine($"Processed {totalRows} rows...");
                    
                    var parts = line.Split(',');
                    if (parts.Length < 7) continue;
                    
                    string ticker = parts[0].Trim().ToUpperInvariant();
                    if (string.IsNullOrEmpty(ticker) || !DateTime.TryParse(parts[1].Trim(), out DateTime date))
                    {
                        missingValueCount++;
                        continue;
                    }
                    
                    if (!decimal.TryParse(parts[2].Trim(), out decimal open)) open = 0;
                    if (!decimal.TryParse(parts[3].Trim(), out decimal high)) high = 0;
                    if (!decimal.TryParse(parts[4].Trim(), out decimal low)) low = 0;
                    if (!decimal.TryParse(parts[5].Trim(), out decimal close)) close = 0;
                    
                    long? volume = null;
                    if (long.TryParse(parts[6].Trim(), out long v)) volume = v;
                    else if (decimal.TryParse(parts[6].Trim(), out decimal vd)) volume = (long)vd;
                    
                    if (open == 0 && high == 0 && low == 0 && close == 0)
                    {
                        zeroPrice++;
                        continue;
                    }
                    
                    if (high < low || high < open || high < close || low > open || low > close)
                    {
                        invalidOhlc++;
                        continue;
                    }
                    
                    string key = $"{ticker}_{date:yyyy-MM-dd}";
                    if (seenRecords.TryGetValue(key, out var existing))
                    {
                        if (existing.Open == open && existing.High == high && existing.Low == low && existing.Close == close && existing.Volume == volume)
                        {
                            exactDuplicates++;
                        }
                        else
                        {
                            conflictingDuplicates++;
                        }
                        continue;
                    }
                    
                    seenRecords[key] = (open, high, low, close, volume);
                    
                    if (ticker.EndsWith("BOND") || ticker.Contains("MUTUAL") || ticker.Contains("FUND"))
                    {
                        ignoredList.Add(ticker);
                        continue;
                    }
                    
                    if (!companyMap.TryGetValue(ticker, out int companyId))
                    {
                        unmatchedCodes++;
                        unmatchedList.Add(ticker);
                        continue;
                    }
                    
                    matchedCodes++;
                    matchedList.Add(ticker);
                    
                    // Idempotency check
                    if (existingRecords.Contains(new { CompanyId = companyId, TradingDate = (DateTime?)date }))
                    {
                        continue; // Skip already imported
                    }
                    
                    if (date < minDate) minDate = date;
                    if (date > maxDate) maxDate = date;
                    
                    validRecords.Add(new CompanyPriceHistory
                    {
                        CompanyId = companyId,
                        TradingDate = date,
                        RecordedAt = date,
                        Price = close,
                        OpenPrice = open,
                        HighPrice = high,
                        LowPrice = low,
                        Volume = volume,
                        Source = "Harvard Dataverse",
                        SourceDataset = "Dhaka Stock Exchange Historical Data",
                        SourceDoi = "10.7910/DVN/XIFYT1",
                        ImportBatchId = batchId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            
            Console.WriteLine($"\n--- IMPORT SUMMARY ---");
            Console.WriteLine($"Total Rows Read: {totalRows}");
            Console.WriteLine($"Zero Price Rows: {zeroPrice}");
            Console.WriteLine($"Invalid OHLC Rows: {invalidOhlc}");
            Console.WriteLine($"Exact Duplicates: {exactDuplicates}");
            Console.WriteLine($"Conflicting Duplicates: {conflictingDuplicates}");
            Console.WriteLine($"Missing Values: {missingValueCount}");
            Console.WriteLine($"Unmatched Trading Codes: {unmatchedCodes}");
            Console.WriteLine($"Valid Matched Records to Import: {validRecords.Count}");
            
            // Generate Machine Readable Report
            var report = new
            {
                TotalRawRows = totalRows,
                ExactDuplicateCount = exactDuplicates,
                ConflictingDuplicateCount = conflictingDuplicates,
                InvalidOhlcCount = invalidOhlc,
                ZeroPriceCount = zeroPrice,
                MissingValueCount = missingValueCount,
                UniqueTradingCodeCount = seenRecords.Keys.Select(k => k.Split('_')[0]).Distinct().Count(),
                MinimumDate = minDate == DateTime.MaxValue ? null : minDate.ToString("yyyy-MM-dd"),
                MaximumDate = maxDate == DateTime.MinValue ? null : maxDate.ToString("yyyy-MM-dd"),
                CandidateCompanyCount = matchedList.Count,
                UnmatchedTickerCount = unmatchedList.Count,
                MatchedCompanies = matchedList.OrderBy(x => x).ToList(),
                UnmatchedCompanies = unmatchedList.OrderBy(x => x).ToList(),
                IgnoredNonCompany = ignoredList.OrderBy(x => x).ToList(),
                AmbiguousCompanies = new List<string>()
            };
            
            string reportJson = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("DataQualityReport.json", reportJson);
            Console.WriteLine("Data quality report saved to DataQualityReport.json");
            
            Console.WriteLine("\nBulk inserting into database...");
            
            try
            {
                // Fast insert using MySqlBulkCopy
                var connection = (MySqlConnection)context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open) connection.Open();
                
                var dataTable = new DataTable("COMPANY_PRICE_HISTORY");
                dataTable.Columns.Add("COMPANY_ID", typeof(int));
                dataTable.Columns.Add("PRICE", typeof(decimal));
                dataTable.Columns.Add("OPEN_PRICE", typeof(decimal));
                dataTable.Columns.Add("HIGH_PRICE", typeof(decimal));
                dataTable.Columns.Add("LOW_PRICE", typeof(decimal));
                dataTable.Columns.Add("VOLUME", typeof(long));
                dataTable.Columns.Add("RECORDED_AT", typeof(DateTime));
                dataTable.Columns.Add("TRADING_DATE", typeof(DateTime));
                dataTable.Columns.Add("SOURCE", typeof(string));
                dataTable.Columns.Add("SOURCE_DATASET", typeof(string));
                dataTable.Columns.Add("SOURCE_DOI", typeof(string));
                dataTable.Columns.Add("IMPORT_BATCH_ID", typeof(string));
                dataTable.Columns.Add("CREATED_AT", typeof(DateTime));
                
                foreach (var rec in validRecords)
                {
                    dataTable.Rows.Add(
                        rec.CompanyId,
                        rec.Price,
                        rec.OpenPrice,
                        rec.HighPrice,
                        rec.LowPrice,
                        (object?)rec.Volume ?? DBNull.Value,
                        rec.RecordedAt,
                        rec.TradingDate,
                        rec.Source,
                        rec.SourceDataset,
                        rec.SourceDoi,
                        rec.ImportBatchId,
                        rec.CreatedAt
                    );
                }
                
                var bulkCopy = new MySqlBulkCopy(connection);
                bulkCopy.DestinationTableName = "COMPANY_PRICE_HISTORY";
                bulkCopy.BulkCopyTimeout = 600;
                
                for (int i = 0; i < dataTable.Columns.Count; i++)
                {
                    bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i, dataTable.Columns[i].ColumnName));
                }
                
                bulkCopy.WriteToServer(dataTable);
                Console.WriteLine("Bulk insert completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during bulk insert: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
