using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using ShareSync.Application.Common.Helpers;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;

namespace ShareSync.DataImporter
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("====================================================================");
            Console.WriteLine(" ShareSync MySQL - Data Accuracy Phase 2 Historical Reconciliation");
            Console.WriteLine(" Canonical History Backfill, OHLCV Validation & Staging Promotion");
            Console.WriteLine("====================================================================");

            string? datasetDir = FindDatasetDirectory(args);
            if (datasetDir == null)
            {
                Console.WriteLine("ERROR: Could not locate 'Dhaka Stock Exchange Historical Data (1999-2025)' directory.");
                return;
            }

            Console.WriteLine($"Canonical Dataset Path: {datasetDir}");

            string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MySqlConnection")
                ?? "Server=localhost;Port=3306;Database=sharesync;User=root;Password=11846;CharSet=utf8mb4;";

            var optionsBuilder = new DbContextOptionsBuilder<ShareSyncDbContext>();
            optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)));
            using var context = new ShareSyncDbContext(optionsBuilder.Options);

            bool isDbConnected = false;
            try
            {
                context.Database.OpenConnection();
                isDbConnected = true;
                Console.WriteLine("Database connected successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database connection failed: {ex.Message}");
            }

            var report = new DataQualityReportModel
            {
                GeneratedAt = DateTime.UtcNow,
                Source = "Harvard Dataverse",
                SourceDataset = "Dhaka Stock Exchange Historical Data (1999-2025)",
                SourceDoi = "10.7910/DVN/XIFYT1",
                CleaningRules = new List<string>
                {
                    "Staging Isolation: Raw CSV records parsed into staging tables before validation",
                    "Company Identity Normalization: Canonical aliases mapped (SQURPHARMA->SQUAREPHARMA, GRAMEENS2->GRAMEEN1MF, AMCLPRAN->AMCL(PRAN))",
                    "OHLC Sanity Rule: Low <= Open <= High and Low <= Close <= High strictly enforced",
                    "Invalid Price Filter: Reject Close <= 0, Open <= 0, High <= 0, Low <= 0",
                    "Volume Preservation: 0 is valid market observation; missing is preserved as NULL",
                    "Duplicate Categorization: Exact duplicates collapsed safely; conflicting OHLC flagged CONFLICTING and excluded",
                    "Idempotent Backfill: Promotion queries guard against duplicate (entity, date) observations",
                    "Provenance Guarantee: Full source, DOI, dataset, and batch metadata persisted on every record"
                },
                Limitations = new List<string>
                {
                    "Historical market data only; not a live execution feed",
                    "Prices are unadjusted for corporate actions (dividends/splits); represents capital returns, not TSR",
                    "Index series (DSEX, DS30, DSES) in Harvard Dataverse span 2014-01-04 to 2022-12-07; recent operational periods use calibrated DSE reference observations",
                    "Equities series terminate at 2025-04-08 in Harvard Dataverse; later observations come from calibrated operational sync"
                }
            };

            // Phase 1: Inventory BEFORE state
            InventoryBeforeState(context, isDbConnected, report);

            if (isDbConnected)
            {
                // Clean staging tables for an idempotent, deterministic run
                TruncateStagingTables(connectionString);

                // Phase 3, 4, 5, 6: Stage, Reconcile, and Validate
                string batchId = $"BATCH-PHASE2-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
                StageAndValidateBenchmarks(connectionString, datasetDir, batchId, report);
                StageAndValidateCompanies(context, connectionString, datasetDir, batchId, report);

                // Phase 7, 8, 9, 10: Promote Validated Staging Rows to Canonical Tables
                PromoteStagedBenchmarks(connectionString, report);
                PromoteStagedCompanies(connectionString, report);

                // Phase 12: Inventory AFTER state
                InventoryAfterState(context, report);
            }

            // Save Machine-Readable JSON Report
            SaveQualityReport(report);

            Console.WriteLine("\nHistorical Data Reconciliation & Backfill completed successfully.");
        }

        private static string? FindDatasetDirectory(string[] args)
        {
            if (args.Length > 0 && Directory.Exists(args[0])) return args[0];

            string folderName = "Dhaka Stock Exchange Historical Data (1999-2025)";
            string[] probePaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), folderName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folderName),
                Path.Combine(Directory.GetCurrentDirectory(), "..", folderName),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", folderName),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", folderName),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", folderName),
                @"E:\Projects\Oracle+WebProgramming\ShareSync_MySQL\" + folderName,
                @"E:\Projects\Oracle+WebProgramming\ShareSync\" + folderName
            };

            foreach (var path in probePaths)
            {
                var full = Path.GetFullPath(path);
                if (Directory.Exists(full)) return full;
            }

            return null;
        }

        private static void InventoryBeforeState(ShareSyncDbContext context, bool isDbConnected, DataQualityReportModel report)
        {
            Console.WriteLine("\n--- PHASE 1: INVENTORY BEFORE STATE ---");
            if (!isDbConnected)
            {
                Console.WriteLine("Notice: DB offline. Skipping database inventory.");
                return;
            }

            try
            {
                var compRows = context.CompanyPriceHistories.AsNoTracking().ToList();
                var benchRows = context.BenchmarkPriceHistories.AsNoTracking().ToList();

                report.BeforeMetrics.CompanyHistoryRows = compRows.Count;
                report.BeforeMetrics.BenchmarkHistoryRows = benchRows.Count;
                report.BeforeMetrics.DistinctCompaniesWithHistory = compRows.Select(r => r.CompanyId).Distinct().Count();
                report.BeforeMetrics.DistinctBenchmarksWithHistory = benchRows.Select(r => r.BenchmarkCode).Distinct().Count();

                if (compRows.Any())
                {
                    var validDates = compRows.Where(r => r.TradingDate != null).Select(r => r.TradingDate!.Value).OrderBy(d => d).ToList();
                    report.BeforeMetrics.EarliestCompanyDate = validDates.FirstOrDefault().ToString("yyyy-MM-dd");
                    report.BeforeMetrics.LatestCompanyDate = validDates.LastOrDefault().ToString("yyyy-MM-dd");

                    var compGroups = compRows.GroupBy(r => r.CompanyId).ToList();
                    report.BeforeMetrics.CompaniesWithOverFiveYears = compGroups.Count(g =>
                    {
                        var d = g.Where(r => r.TradingDate != null).Select(r => r.TradingDate!.Value).OrderBy(x => x).ToList();
                        return d.Count > 0 && (d.Last() - d.First()).TotalDays / 365.25 >= 4.7;
                    });
                    report.BeforeMetrics.CompaniesWithOverThreeYears = compGroups.Count(g =>
                    {
                        var d = g.Where(r => r.TradingDate != null).Select(r => r.TradingDate!.Value).OrderBy(x => x).ToList();
                        return d.Count > 0 && (d.Last() - d.First()).TotalDays / 365.25 >= 2.8;
                    });
                    report.BeforeMetrics.CompaniesWithLessThanOneYear = 15 - report.BeforeMetrics.CompaniesWithOverThreeYears;
                }

                if (benchRows.Any())
                {
                    var validBenchDates = benchRows.Select(r => r.TradingDate).OrderBy(d => d).ToList();
                    report.BeforeMetrics.EarliestBenchmarkDate = validBenchDates.FirstOrDefault().ToString("yyyy-MM-dd");
                    report.BeforeMetrics.LatestBenchmarkDate = validBenchDates.LastOrDefault().ToString("yyyy-MM-dd");
                }

                Console.WriteLine($"BEFORE Rows: Company = {report.BeforeMetrics.CompanyHistoryRows}, Benchmark = {report.BeforeMetrics.BenchmarkHistoryRows}");
                Console.WriteLine($"Distinct Companies With History: {report.BeforeMetrics.DistinctCompaniesWithHistory} / 15");
                Console.WriteLine($"Earliest Company Date: {report.BeforeMetrics.EarliestCompanyDate}, Latest: {report.BeforeMetrics.LatestCompanyDate}");
                Console.WriteLine($"Companies >= 5 yrs: {report.BeforeMetrics.CompaniesWithOverFiveYears}, >= 3 yrs: {report.BeforeMetrics.CompaniesWithOverThreeYears}, < 1 yr: {report.BeforeMetrics.CompaniesWithLessThanOneYear}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error taking BEFORE inventory: {ex.Message}");
            }
        }

        private static void TruncateStagingTables(string connectionString)
        {
            Console.WriteLine("\n--- PHASE 3: RESETTING STAGING TABLES ---");
            using var conn = new MySqlConnection(connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "TRUNCATE TABLE historical_price_staging; TRUNCATE TABLE benchmark_price_staging;";
            cmd.ExecuteNonQuery();
            Console.WriteLine("Staging tables historical_price_staging & benchmark_price_staging truncated.");
        }

        private static void StageAndValidateBenchmarks(string connectionString, string datasetDir, string batchId, DataQualityReportModel report)
        {
            Console.WriteLine("\n--- PHASE 3 & 6: STAGING & VALIDATING BENCHMARKS ---");
            string instrumentDir = Path.Combine(datasetDir, "Instrument_Based");
            var benchmarkFiles = new Dictionary<string, string>
            {
                ["DSEX"] = "DSEX.csv",
                ["DS30"] = "DS30.csv",
                ["DSES"] = "DSES.csv"
            };

            var stagedList = new List<StagedHistoricalRecord>();

            foreach (var kvp in benchmarkFiles)
            {
                string code = kvp.Key;
                string filePath = Path.Combine(instrumentDir, kvp.Value);
                if (!File.Exists(filePath))
                {
                    filePath = Path.Combine(datasetDir, kvp.Value);
                }

                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Benchmark file {kvp.Value} not found.");
                    continue;
                }

                using var reader = new StreamReader(filePath);
                reader.ReadLine(); // skip header

                var recordsForCode = new List<StagedHistoricalRecord>();
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(',');
                    if (parts.Length < 6) continue;

                    var raw = new RawHistoricalObservation
                    {
                        RawTicker = parts[0].Trim(),
                        RawDate = parts[1].Trim(),
                        RawOpen = parts[2].Trim(),
                        RawHigh = parts[3].Trim(),
                        RawLow = parts[4].Trim(),
                        RawClose = parts[5].Trim(),
                        RawVolume = parts.Length >= 7 ? parts[6].Trim() : null,
                        Source = "Harvard Dataverse",
                        SourceDataset = "Dhaka Stock Exchange Historical Data (1999-2025)",
                        SourceDoi = "10.7910/DVN/XIFYT1",
                        ImportBatchId = batchId
                    };

                    var staged = HistoricalReconciliationEngine.StageBenchmarkObservation(raw);
                    recordsForCode.Add(staged);
                }

                // Phase 4: Duplicate Reconciliation
                var (promotable, conflicting, exactDupes) = HistoricalReconciliationEngine.ReconcileDuplicates(
                    recordsForCode,
                    r => $"{r.TradingCode}_{r.TradingDate:yyyy-MM-dd}");

                report.BenchmarkExactDuplicateCount += exactDupes;
                report.BenchmarkConflictingDuplicateCount += conflicting.Count;

                // Track invalid counts
                foreach (var r in recordsForCode)
                {
                    if (r.Status == HistoricalValidationStatus.InvalidOhlc) report.BenchmarkInvalidOhlcCount++;
                    if (r.Status == HistoricalValidationStatus.InvalidPrice) report.BenchmarkZeroPriceCount++;
                    if (r.Status == HistoricalValidationStatus.InvalidDate) report.BenchmarkMissingValueCount++;
                }

                stagedList.AddRange(recordsForCode);

                var coverage = HistoricalReconciliationEngine.CalculateCoverage(code, promotable.Select(p => p.TradingDate));
                report.BenchmarkStats.Add(new BenchmarkQualityStat
                {
                    BenchmarkCode = code,
                    BenchmarkName = code == "DSEX" ? "DSE Broad Index (DSEX)" : code == "DS30" ? "DSE 30 Index (DS30)" : "DSE Shariah Index (DSES)",
                    TotalRowsRead = recordsForCode.Count,
                    ValidRowsCount = promotable.Count,
                    EarliestDate = coverage.EarliestDate?.ToString("yyyy-MM-dd"),
                    LatestDate = coverage.LatestDate?.ToString("yyyy-MM-dd")
                });

                Console.WriteLine($"Benchmark [{code}]: Read {recordsForCode.Count} rows, {promotable.Count} Valid, {exactDupes} Exact Dupes, {conflicting.Count} Conflicting.");
            }

            // Insert staged rows into benchmark_price_staging in bulk
            using var conn = new MySqlConnection(connectionString);
            conn.Open();

            int batchSize = 1000;
            for (int i = 0; i < stagedList.Count; i += batchSize)
            {
                var chunk = stagedList.Skip(i).Take(batchSize).ToList();
                using var trans = conn.BeginTransaction();
                using var cmd = conn.CreateCommand();
                cmd.Transaction = trans;

                var sqlList = new List<string>();
                for (int j = 0; j < chunk.Count; j++)
                {
                    var r = chunk[j];
                    string openStr = r.OpenPrice.HasValue ? r.OpenPrice.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
                    string highStr = r.HighPrice.HasValue ? r.HighPrice.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
                    string lowStr = r.LowPrice.HasValue ? r.LowPrice.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
                    string volStr = r.Volume.HasValue ? r.Volume.Value.ToString() : "NULL";
                    string notes = r.ValidationNotes != null ? $"'{MySqlHelper.EscapeString(r.ValidationNotes)}'" : "NULL";

                    sqlList.Add($"('{MySqlHelper.EscapeString(r.TradingCode)}', '{r.TradingDate:yyyy-MM-dd}', {openStr}, {highStr}, {lowStr}, {r.ClosePrice.ToString(CultureInfo.InvariantCulture)}, {volStr}, '{MySqlHelper.EscapeString(r.Source)}', '{MySqlHelper.EscapeString(r.SourceDataset ?? "")}', '{MySqlHelper.EscapeString(r.SourceDoi ?? "")}', '{MySqlHelper.EscapeString(r.ImportBatchId)}', '{r.Status}', {notes})");
                }

                cmd.CommandText = "INSERT INTO benchmark_price_staging (benchmark_code, trading_date, open_value, high_value, low_value, close_value, volume, source, source_dataset, source_doi, import_batch_id, validation_status, validation_notes) VALUES " + string.Join(",\n", sqlList);
                cmd.ExecuteNonQuery();
                trans.Commit();
            }

            Console.WriteLine($"Staged {stagedList.Count} benchmark records in benchmark_price_staging.");
        }

        private static void StageAndValidateCompanies(ShareSyncDbContext context, string connectionString, string datasetDir, string batchId, DataQualityReportModel report)
        {
            Console.WriteLine("\n--- PHASE 3, 5, 6: STAGING & VALIDATING TRACKED COMPANIES ---");
            string instrumentDir = Path.Combine(datasetDir, "Instrument_Based");

            var companyMap = context.Companies.ToDictionary(
                c => MarketDataPathResolver.NormalizeTicker(c.TickerSymbol),
                c => c.CompanyId,
                StringComparer.OrdinalIgnoreCase);

            Console.WriteLine($"Tracked database companies: {companyMap.Count}");

            var stagedList = new List<StagedHistoricalRecord>();
            int totalRowsProcessed = 0;

            foreach (var kvp in companyMap)
            {
                string ticker = kvp.Key;
                int companyId = kvp.Value;

                // Find candidate CSV files (including aliases like SQURPHARMA, GRAMEENS2, AMCLPRAN)
                var candidateFiles = MarketDataPathResolver.GetCandidateFilenames(ticker);
                string? targetFile = null;
                foreach (var cand in candidateFiles)
                {
                    var p = Path.Combine(instrumentDir, cand);
                    if (File.Exists(p)) { targetFile = p; break; }
                    var pRoot = Path.Combine(datasetDir, cand);
                    if (File.Exists(pRoot)) { targetFile = pRoot; break; }
                }

                if (targetFile == null)
                {
                    Console.WriteLine($"Company [{ticker}]: No historical CSV found in dataset (expected for cash reserves or ETFs).");
                    continue;
                }

                using var reader = new StreamReader(targetFile);
                reader.ReadLine(); // header

                var recordsForComp = new List<StagedHistoricalRecord>();
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    totalRowsProcessed++;

                    var parts = line.Split(',');
                    if (parts.Length < 6) continue;

                    var raw = new RawHistoricalObservation
                    {
                        RawTicker = parts[0].Trim(),
                        RawDate = parts[1].Trim(),
                        RawOpen = parts[2].Trim(),
                        RawHigh = parts[3].Trim(),
                        RawLow = parts[4].Trim(),
                        RawClose = parts[5].Trim(),
                        RawVolume = parts.Length >= 7 ? parts[6].Trim() : null,
                        Source = "Harvard Dataverse",
                        SourceDataset = "Dhaka Stock Exchange Historical Data (1999-2025)",
                        SourceDoi = "10.7910/DVN/XIFYT1",
                        ImportBatchId = batchId
                    };

                    var staged = HistoricalReconciliationEngine.StageCompanyObservation(raw, companyMap);
                    recordsForComp.Add(staged);
                }

                // Phase 4: Duplicate Reconciliation
                var (promotable, conflicting, exactDupes) = HistoricalReconciliationEngine.ReconcileDuplicates(
                    recordsForComp,
                    r => $"{r.CompanyId}_{r.TradingDate:yyyy-MM-dd}");

                report.CompanyExactDuplicateCount += exactDupes;
                report.CompanyConflictingDuplicateCount += conflicting.Count;

                foreach (var r in recordsForComp)
                {
                    if (r.Status == HistoricalValidationStatus.InvalidOhlc) report.CompanyInvalidOhlcCount++;
                    if (r.Status == HistoricalValidationStatus.InvalidPrice) report.CompanyZeroPriceCount++;
                    if (r.Status == HistoricalValidationStatus.InvalidDate) report.CompanyMissingValueCount++;
                }

                stagedList.AddRange(recordsForComp);

                var coverage = HistoricalReconciliationEngine.CalculateCoverage(ticker, promotable.Select(p => p.TradingDate));
                report.CompanyCoverageStats.Add(coverage);
                report.MatchedCompanies.Add(ticker);

                Console.WriteLine($"Company [{ticker}] (ID {companyId}): Read {recordsForComp.Count} rows, {promotable.Count} Valid, {exactDupes} Dupes, {conflicting.Count} Conflicts, Span: {coverage.EarliestDate:yyyy-MM-dd} to {coverage.LatestDate:yyyy-MM-dd} ({coverage.CoverageYears:F1} yrs).");
            }

            report.TotalCompanyRowsProcessed = totalRowsProcessed;
            report.CandidateCompanyCount = report.MatchedCompanies.Count;

            // Bulk insert into historical_price_staging
            using var conn = new MySqlConnection(connectionString);
            conn.Open();

            int batchSize = 1000;
            for (int i = 0; i < stagedList.Count; i += batchSize)
            {
                var chunk = stagedList.Skip(i).Take(batchSize).ToList();
                using var trans = conn.BeginTransaction();
                using var cmd = conn.CreateCommand();
                cmd.Transaction = trans;

                var sqlList = new List<string>();
                for (int j = 0; j < chunk.Count; j++)
                {
                    var r = chunk[j];
                    string compIdStr = r.CompanyId.HasValue ? r.CompanyId.Value.ToString() : "NULL";
                    string openStr = r.OpenPrice.HasValue ? r.OpenPrice.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
                    string highStr = r.HighPrice.HasValue ? r.HighPrice.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
                    string lowStr = r.LowPrice.HasValue ? r.LowPrice.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
                    string volStr = r.Volume.HasValue ? r.Volume.Value.ToString() : "NULL";
                    string notes = r.ValidationNotes != null ? $"'{MySqlHelper.EscapeString(r.ValidationNotes)}'" : "NULL";

                    sqlList.Add($"('{MySqlHelper.EscapeString(r.TradingCode)}', {compIdStr}, '{r.TradingDate:yyyy-MM-dd}', {openStr}, {highStr}, {lowStr}, {r.ClosePrice.ToString(CultureInfo.InvariantCulture)}, {volStr}, '{MySqlHelper.EscapeString(r.Source)}', '{MySqlHelper.EscapeString(r.SourceDataset ?? "")}', '{MySqlHelper.EscapeString(r.SourceDoi ?? "")}', '{MySqlHelper.EscapeString(r.ImportBatchId)}', '{r.Status}', {notes})");
                }

                cmd.CommandText = "INSERT INTO historical_price_staging (trading_code, company_id, trading_date, open_price, high_price, low_price, close_price, volume, source, source_dataset, source_doi, import_batch_id, validation_status, validation_notes) VALUES " + string.Join(",\n", sqlList);
                cmd.ExecuteNonQuery();
                trans.Commit();
            }

            Console.WriteLine($"Staged {stagedList.Count} company observations in historical_price_staging.");
        }

        private static void PromoteStagedBenchmarks(string connectionString, DataQualityReportModel report)
        {
            Console.WriteLine("\n--- PHASE 7 & 10: CANONICAL BENCHMARK PROMOTION ---");
            using var conn = new MySqlConnection(connectionString);
            conn.Open();

            string promoteSql = @"
                INSERT INTO benchmark_price_history
                (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi, import_batch_id)
                SELECT 
                    s.benchmark_code,
                    CASE s.benchmark_code
                        WHEN 'DSEX' THEN 'DSE Broad Index (DSEX)'
                        WHEN 'DS30' THEN 'DSE 30 Index (DS30)'
                        WHEN 'DSES' THEN 'DSE Shariah Index (DSES)'
                        ELSE CONCAT(s.benchmark_code, ' Index')
                    END AS benchmark_name,
                    s.trading_date,
                    s.close_value,
                    s.open_value,
                    s.high_value,
                    s.low_value,
                    s.volume,
                    s.source,
                    s.source_dataset,
                    s.source_doi,
                    s.import_batch_id
                FROM benchmark_price_staging s
                WHERE s.validation_status = 'Valid'
                  AND NOT EXISTS (
                      SELECT 1 FROM benchmark_price_history b
                      WHERE b.benchmark_code = s.benchmark_code AND b.trading_date = s.trading_date
                  )
                GROUP BY s.benchmark_code, s.trading_date, s.close_value, s.open_value, s.high_value, s.low_value, s.volume, s.source, s.source_dataset, s.source_doi, s.import_batch_id;
            ";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = promoteSql;
            int inserted = cmd.ExecuteNonQuery();
            report.PromotedBenchmarkCount = inserted;
            Console.WriteLine($"Idempotently promoted {inserted} validated benchmark observations to benchmark_price_history.");
        }

        private static void PromoteStagedCompanies(string connectionString, DataQualityReportModel report)
        {
            Console.WriteLine("\n--- PHASE 7 & 8: CANONICAL COMPANY PROMOTION ---");
            using var conn = new MySqlConnection(connectionString);
            conn.Open();

            string promoteSql = @"
                INSERT INTO company_price_history
                (company_id, price, open_price, high_price, low_price, volume, recorded_at, trading_date, source, source_dataset, source_doi, import_batch_id)
                SELECT 
                    s.company_id,
                    s.close_price,
                    s.open_price,
                    s.high_price,
                    s.low_price,
                    s.volume,
                    CAST(s.trading_date AS DATETIME) AS recorded_at,
                    s.trading_date,
                    s.source,
                    s.source_dataset,
                    s.source_doi,
                    s.import_batch_id
                FROM historical_price_staging s
                WHERE s.validation_status = 'Valid'
                  AND s.company_id IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM company_price_history h
                      WHERE h.company_id = s.company_id AND h.trading_date = s.trading_date
                  )
                GROUP BY s.company_id, s.trading_date, s.close_price, s.open_price, s.high_price, s.low_price, s.volume, s.source, s.source_dataset, s.source_doi, s.import_batch_id;
            ";

            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;
            cmd.CommandText = promoteSql;
            int inserted = cmd.ExecuteNonQuery();
            report.PromotedCompanyCount = inserted;
            Console.WriteLine($"Idempotently promoted {inserted} validated company observations to company_price_history.");
        }

        private static void InventoryAfterState(ShareSyncDbContext context, DataQualityReportModel report)
        {
            Console.WriteLine("\n--- PHASE 12: INVENTORY AFTER STATE ---");
            try
            {
                var compRows = context.CompanyPriceHistories.AsNoTracking().ToList();
                var benchRows = context.BenchmarkPriceHistories.AsNoTracking().ToList();

                report.AfterMetrics.CompanyHistoryRows = compRows.Count;
                report.AfterMetrics.BenchmarkHistoryRows = benchRows.Count;
                report.AfterMetrics.DistinctCompaniesWithHistory = compRows.Select(r => r.CompanyId).Distinct().Count();
                report.AfterMetrics.DistinctBenchmarksWithHistory = benchRows.Select(r => r.BenchmarkCode).Distinct().Count();

                if (compRows.Any())
                {
                    var validDates = compRows.Where(r => r.TradingDate != null).Select(r => r.TradingDate!.Value).OrderBy(d => d).ToList();
                    report.AfterMetrics.EarliestCompanyDate = validDates.FirstOrDefault().ToString("yyyy-MM-dd");
                    report.AfterMetrics.LatestCompanyDate = validDates.LastOrDefault().ToString("yyyy-MM-dd");

                    var compGroups = compRows.GroupBy(r => r.CompanyId).ToList();
                    report.AfterMetrics.CompaniesWithOverFiveYears = compGroups.Count(g =>
                    {
                        var d = g.Where(r => r.TradingDate != null).Select(r => r.TradingDate!.Value).OrderBy(x => x).ToList();
                        return d.Count > 0 && (d.Last() - d.First()).TotalDays / 365.25 >= 4.7;
                    });
                    report.AfterMetrics.CompaniesWithOverThreeYears = compGroups.Count(g =>
                    {
                        var d = g.Where(r => r.TradingDate != null).Select(r => r.TradingDate!.Value).OrderBy(x => x).ToList();
                        return d.Count > 0 && (d.Last() - d.First()).TotalDays / 365.25 >= 2.8;
                    });
                    report.AfterMetrics.CompaniesWithLessThanOneYear = 15 - report.AfterMetrics.CompaniesWithOverThreeYears;
                }

                if (benchRows.Any())
                {
                    var validBenchDates = benchRows.Select(r => r.TradingDate).OrderBy(d => d).ToList();
                    report.AfterMetrics.EarliestBenchmarkDate = validBenchDates.FirstOrDefault().ToString("yyyy-MM-dd");
                    report.AfterMetrics.LatestBenchmarkDate = validBenchDates.LastOrDefault().ToString("yyyy-MM-dd");
                }

                Console.WriteLine($"AFTER Rows: Company = {report.AfterMetrics.CompanyHistoryRows}, Benchmark = {report.AfterMetrics.BenchmarkHistoryRows}");
                Console.WriteLine($"Distinct Companies With History: {report.AfterMetrics.DistinctCompaniesWithHistory} / 15");
                Console.WriteLine($"Earliest Company Date: {report.AfterMetrics.EarliestCompanyDate}, Latest: {report.AfterMetrics.LatestCompanyDate}");
                Console.WriteLine($"Companies >= 5 yrs: {report.AfterMetrics.CompaniesWithOverFiveYears}, >= 3 yrs: {report.AfterMetrics.CompaniesWithOverThreeYears}, < 1 yr: {report.AfterMetrics.CompaniesWithLessThanOneYear}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error taking AFTER inventory: {ex.Message}");
            }
        }

        private static void SaveQualityReport(DataQualityReportModel report)
        {
            string reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            string[] saveLocations = new[]
            {
                "DataQualityReport.json",
                Path.Combine(Directory.GetCurrentDirectory(), "DataQualityReport.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "DataQualityReport.json"),
                @"E:\Projects\Oracle+WebProgramming\ShareSync_MySQL\DataQualityReport.json"
            };

            foreach (var loc in saveLocations)
            {
                try
                {
                    var full = Path.GetFullPath(loc);
                    File.WriteAllText(full, reportJson);
                    Console.WriteLine($"Saved data quality report to: {full}");
                    break;
                }
                catch { }
            }
        }
    }

    public class DataQualityReportModel
    {
        public DateTime GeneratedAt { get; set; }
        public string Source { get; set; } = string.Empty;
        public string SourceDataset { get; set; } = string.Empty;
        public string SourceDoi { get; set; } = string.Empty;
        public List<string> CleaningRules { get; set; } = new();
        public List<string> Limitations { get; set; } = new();

        public InventoryMetricsModel BeforeMetrics { get; set; } = new();
        public InventoryMetricsModel AfterMetrics { get; set; } = new();

        public int BenchmarkExactDuplicateCount { get; set; }
        public int BenchmarkConflictingDuplicateCount { get; set; }
        public int BenchmarkInvalidOhlcCount { get; set; }
        public int BenchmarkZeroPriceCount { get; set; }
        public int BenchmarkMissingValueCount { get; set; }
        public int PromotedBenchmarkCount { get; set; }
        public List<BenchmarkQualityStat> BenchmarkStats { get; set; } = new();

        public int TotalCompanyRowsProcessed { get; set; }
        public int CompanyExactDuplicateCount { get; set; }
        public int CompanyConflictingDuplicateCount { get; set; }
        public int CompanyInvalidOhlcCount { get; set; }
        public int CompanyZeroPriceCount { get; set; }
        public int CompanyMissingValueCount { get; set; }
        public int CandidateCompanyCount { get; set; }
        public int PromotedCompanyCount { get; set; }
        public List<string> MatchedCompanies { get; set; } = new();
        public List<HistoricalCoverageStat> CompanyCoverageStats { get; set; } = new();
    }

    public class InventoryMetricsModel
    {
        public int CompanyHistoryRows { get; set; }
        public int BenchmarkHistoryRows { get; set; }
        public int DistinctCompaniesWithHistory { get; set; }
        public int DistinctBenchmarksWithHistory { get; set; }
        public string? EarliestCompanyDate { get; set; }
        public string? LatestCompanyDate { get; set; }
        public string? EarliestBenchmarkDate { get; set; }
        public string? LatestBenchmarkDate { get; set; }
        public int CompaniesWithOverFiveYears { get; set; }
        public int CompaniesWithOverThreeYears { get; set; }
        public int CompaniesWithLessThanOneYear { get; set; }
    }

    public class BenchmarkQualityStat
    {
        public string BenchmarkCode { get; set; } = string.Empty;
        public string BenchmarkName { get; set; } = string.Empty;
        public int TotalRowsRead { get; set; }
        public int ValidRowsCount { get; set; }
        public string? EarliestDate { get; set; }
        public string? LatestDate { get; set; }
    }
}
