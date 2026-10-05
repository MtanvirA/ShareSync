using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Transactions;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Services;

public class CsvTransactionImportService : ICsvTransactionImportService
{
    private const int MaxAllowedRows = 1000;
    private const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IApplicationDbContext _context;

    public CsvTransactionImportService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<CsvImportPreviewDto>> ValidateCsvAsync(
        int portfolioId,
        string csvContent,
        int userId,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        var preview = await ParseAndValidateCsvAsync(portfolioId, csvContent, userId, fileName, cancellationToken);
        var message = preview.IsValid
            ? $"CSV validation successful: {preview.ValidRowsCount} transaction(s) ready for import."
            : $"CSV validation found {preview.InvalidRowsCount} error(s) across {preview.TotalRows} row(s).";

        return ApiResponse<CsvImportPreviewDto>.Ok(preview, message);
    }

    public async Task<ApiResponse<CsvImportResultDto>> ExecuteCsvImportAsync(
        int portfolioId,
        string csvContent,
        bool allowPartialImport,
        int userId,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate the CSV input
        var preview = await ParseAndValidateCsvAsync(portfolioId, csvContent, userId, null, cancellationToken);

        // 2. Enforce Atomic Import policy (default)
        if (!allowPartialImport && preview.InvalidRowsCount > 0)
        {
            throw new BusinessRuleException(
                $"Atomic import rejected: The file contains {preview.InvalidRowsCount} invalid row(s). No records were inserted into the database. Enable partial import mode if you wish to import valid rows only."
            );
        }

        var candidateRows = allowPartialImport
            ? preview.Rows.Where(r => r.IsValid).ToList()
            : preview.Rows;

        if (candidateRows.Count == 0)
        {
            throw new BusinessRuleException("No valid transaction rows found to import.");
        }

        // 3. Execute atomic DB transaction
        var insertedTransactions = new List<TransactionDto>();
        decimal totalAmount = 0;

        await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var row in candidateRows)
            {
                if (!row.IsValid || !row.CompanyId.HasValue || !row.TransactionDate.HasValue)
                {
                    continue;
                }

                var transaction = new Transaction
                {
                    PortfolioId = portfolioId,
                    CompanyId = row.CompanyId.Value,
                    TransactionType = row.TransactionType,
                    Quantity = row.Quantity,
                    PricePerShare = row.PricePerShare,
                    TransactionDate = row.TransactionDate.Value
                };

                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync(cancellationToken);

                totalAmount += Math.Round(row.Quantity * row.PricePerShare, 2);

                // Create audit record
                var audit = new TransactionAudit
                {
                    TransactionId = transaction.TransactionId,
                    ActionType = "INSERT",
                    ActionDate = DateTime.UtcNow,
                    ChangedBy = $"User #{userId} (CSV Import)",
                    Details = $"[CSV Import] {row.TransactionType} {row.Quantity:N4} shares of {row.CompanyTicker} @ ৳{row.PricePerShare:N2}. Total: ৳{row.TotalAmount:N2}. {row.Notes}".Trim()
                };

                _context.TransactionAudits.Add(audit);
                await _context.SaveChangesAsync(cancellationToken);

                insertedTransactions.Add(new TransactionDto
                {
                    TransactionId = transaction.TransactionId,
                    PortfolioId = portfolioId,
                    PortfolioName = preview.PortfolioName,
                    CompanyId = row.CompanyId.Value,
                    CompanyName = row.CompanyName ?? row.CompanyTicker,
                    TickerSymbol = row.CompanyTicker,
                    TransactionType = row.TransactionType,
                    Quantity = row.Quantity,
                    PricePerShare = row.PricePerShare,
                    TotalAmount = Math.Round(row.Quantity * row.PricePerShare, 2),
                    TransactionDate = transaction.TransactionDate
                });
            }

            // Commit the entire transaction
            await dbTransaction.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }

        var skippedRows = preview.Rows
            .Where(r => !r.IsValid)
            .SelectMany(r => r.Errors)
            .ToList();

        var result = new CsvImportResultDto
        {
            PortfolioId = portfolioId,
            PortfolioName = preview.PortfolioName,
            TotalProcessed = preview.TotalRows,
            SuccessCount = insertedTransactions.Count,
            SkippedCount = preview.TotalRows - insertedTransactions.Count,
            TotalAmount = totalAmount,
            ImportMode = allowPartialImport ? "Partial" : "Atomic",
            ImportedTransactions = insertedTransactions,
            SkippedRows = skippedRows,
            Message = $"Successfully imported {insertedTransactions.Count} transaction(s) into '{preview.PortfolioName}'."
        };

        return ApiResponse<CsvImportResultDto>.Ok(result, result.Message);
    }

    public string GenerateSampleCsvTemplate()
    {
        var sb = new StringBuilder();
        sb.AppendLine("transaction_date,company_ticker,transaction_type,quantity,price_per_share,notes");
        sb.AppendLine("2026-10-01,GP,BUY,100,410.00,Initial position in Grameenphone");
        sb.AppendLine("2026-10-02,BATBC,BUY,50,518.50,Long-term dividend holding");
        sb.AppendLine("2026-10-05,GP,SELL,20,425.00,Partial profit taking");
        return sb.ToString();
    }

    private async Task<CsvImportPreviewDto> ParseAndValidateCsvAsync(
        int portfolioId,
        string csvContent,
        int userId,
        string? fileName,
        CancellationToken cancellationToken)
    {
        // Security checks: file size & type
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (ext != ".csv" && ext != ".txt")
            {
                throw new AppException("Invalid file type. Only .csv or .txt files are supported.", 400);
            }
        }

        if (string.IsNullOrWhiteSpace(csvContent))
        {
            throw new AppException("The uploaded CSV file is empty.", 400);
        }

        var byteCount = Encoding.UTF8.GetByteCount(csvContent);
        if (byteCount > MaxFileSizeBytes)
        {
            throw new AppException($"File size exceeds the 5 MB limit (Current: {byteCount / (1024 * 1024):N1} MB).", 400);
        }

        // Validate portfolio ownership
        var portfolio = await _context.Portfolios
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PortfolioId == portfolioId, cancellationToken);

        if (portfolio == null)
        {
            throw new NotFoundException("Portfolio", portfolioId);
        }

        if (portfolio.UserId != userId)
        {
            throw new AppException("You do not have permission to import into this portfolio.", 403);
        }

        // Load reference data
        var companies = await _context.Companies
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var companyMap = companies
            .Where(c => !string.IsNullOrWhiteSpace(c.TickerSymbol))
            .ToDictionary(c => c.TickerSymbol.Trim().ToUpperInvariant(), c => c);

        // Load existing portfolio transactions to track holdings & check sell availability
        var existingTransactions = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.PortfolioId == portfolioId)
            .ToListAsync(cancellationToken);

        var runningHoldings = new Dictionary<int, decimal>();
        foreach (var group in existingTransactions.GroupBy(t => t.CompanyId))
        {
            var buyTotal = group.Where(t => t.TransactionType == "BUY").Sum(t => t.Quantity);
            var sellTotal = group.Where(t => t.TransactionType == "SELL").Sum(t => t.Quantity);
            runningHoldings[group.Key] = Math.Max(0, buyTotal - sellTotal);
        }

        // Parse CSV records
        var records = ParseCsvLines(csvContent);
        if (records.Count == 0)
        {
            throw new AppException("The CSV file contains no data rows.", 400);
        }

        var headerTokens = records[0];
        var headerMap = MapHeaders(headerTokens);

        // Verify required columns
        var missingHeaders = GetMissingRequiredHeaders(headerMap);
        if (missingHeaders.Count > 0)
        {
            throw new AppException(
                $"Missing required header column(s): {string.Join(", ", missingHeaders)}. Required columns: transaction_date, company_ticker, transaction_type, quantity, price_per_share.",
                400
            );
        }

        var dataRows = records.Skip(1).ToList();
        if (dataRows.Count == 0)
        {
            throw new AppException("The CSV file contains only headers with no data rows.", 400);
        }

        if (dataRows.Count > MaxAllowedRows)
        {
            throw new AppException($"The CSV file contains {dataRows.Count} rows, exceeding the limit of {MaxAllowedRows} rows.", 400);
        }

        var preview = new CsvImportPreviewDto
        {
            PortfolioId = portfolioId,
            PortfolioName = portfolio.PortfolioName,
            TotalRows = dataRows.Count
        };

        var seenSignatures = new Dictionary<string, int>(); // Signature -> First row number seen
        int rowNumber = 1;

        foreach (var rawTokens in dataRows)
        {
            var rowPreview = new CsvTransactionRowPreviewDto
            {
                RowNumber = rowNumber
            };

            // Check column count
            if (rawTokens.Count != headerTokens.Count)
            {
                rowPreview.Errors.Add(new CsvRowErrorDto
                {
                    RowNumber = rowNumber,
                    Field = "csv_structure",
                    Problem = $"Row has {rawTokens.Count} columns but header has {headerTokens.Count} columns.",
                    SuggestedCorrection = "Ensure all columns are properly separated by commas and enclosed in quotes if they contain commas."
                });
            }

            // Extract values
            string rawDate = GetColumnValue(rawTokens, headerMap, "date");
            string rawTicker = SanitizeFormulaInjection(GetColumnValue(rawTokens, headerMap, "ticker"));
            string rawType = SanitizeFormulaInjection(GetColumnValue(rawTokens, headerMap, "type"));
            string rawQty = GetColumnValue(rawTokens, headerMap, "quantity");
            string rawPrice = GetColumnValue(rawTokens, headerMap, "price");
            string rawNotes = SanitizeFormulaInjection(GetColumnValue(rawTokens, headerMap, "notes"));

            rowPreview.DateString = rawDate;
            rowPreview.CompanyTicker = rawTicker;
            rowPreview.TransactionType = rawType.Trim().ToUpperInvariant();
            rowPreview.Notes = string.IsNullOrWhiteSpace(rawNotes) ? null : rawNotes;

            // 1. Validate Date
            DateTime parsedDate = default;
            bool dateValid = false;
            if (string.IsNullOrWhiteSpace(rawDate))
            {
                rowPreview.Errors.Add(new CsvRowErrorDto
                {
                    RowNumber = rowNumber,
                    Field = "transaction_date",
                    Problem = "Transaction date is required and cannot be empty.",
                    SuggestedCorrection = "Provide a date in YYYY-MM-DD format (e.g., 2026-10-01)."
                });
            }
            else if (!TryParseDate(rawDate, out parsedDate))
            {
                rowPreview.Errors.Add(new CsvRowErrorDto
                {
                    RowNumber = rowNumber,
                    Field = "transaction_date",
                    Problem = $"Invalid date format '{rawDate}'.",
                    SuggestedCorrection = "Use ISO YYYY-MM-DD or MM/DD/YYYY format."
                });
            }
            else if (parsedDate.Date > DateTime.UtcNow.Date.AddDays(1))
            {
                rowPreview.Errors.Add(new CsvRowErrorDto
                {
                    RowNumber = rowNumber,
                    Field = "transaction_date",
                    Problem = $"Transaction date '{rawDate}' cannot be in the future.",
                    SuggestedCorrection = "Enter a date on or before the current date."
                });
            }
            else
            {
                dateValid = true;
                rowPreview.TransactionDate = parsedDate;
            }

            // 2. Validate Ticker & Company Existence
            Company? matchedCompany = null;
            if (string.IsNullOrWhiteSpace(rawTicker))
            {
                rowPreview.Errors.Add(new CsvRowErrorDto
                {
                    RowNumber = rowNumber,
                    Field = "company_ticker",
                    Problem = "Company ticker symbol is missing or empty.",
                    SuggestedCorrection = "Enter a valid DSE ticker symbol such as GP, BATBC, or BEXIMCO."
                });
            }
            else
            {
                var upperTicker = rawTicker.Trim().ToUpperInvariant();
                if (!companyMap.TryGetValue(upperTicker, out matchedCompany))
                {
                    rowPreview.Errors.Add(new CsvRowErrorDto
                    {
                        RowNumber = rowNumber,
                        Field = "company_ticker",
                        Problem = $"Company ticker '{rawTicker}' was not found in the DSE catalog.",
                        SuggestedCorrection = "Check the ticker spelling or search the Companies catalog for the correct DSE symbol."
                    });
                }
                else
                {
                    rowPreview.CompanyId = matchedCompany.CompanyId;
                    rowPreview.CompanyName = matchedCompany.CompanyName;
                    rowPreview.CompanyTicker = matchedCompany.TickerSymbol;
                }
            }

            // 3. Validate Transaction Type
            var normalizedType = rowPreview.TransactionType;
            if (string.IsNullOrWhiteSpace(normalizedType))
            {
                rowPreview.Errors.Add(new CsvRowErrorDto
                {
                    RowNumber = rowNumber,
                    Field = "transaction_type",
                    Problem = "Transaction type is missing.",
                    SuggestedCorrection = "Enter either 'BUY' or 'SELL'."
                });
            }
            else if (normalizedType != "BUY" && normalizedType != "SELL")
            {
                rowPreview.Errors.Add(new CsvRowErrorDto
                {
                    RowNumber = rowNumber,
                    Field = "transaction_type",
                    Problem = $"Invalid transaction type '{rawType}'. Supported values are 'BUY' or 'SELL'.",
                    SuggestedCorrection = "Change transaction type to 'BUY' or 'SELL'."
                });
            }

            // 4. Validate Quantity
            bool qtyValid = false;
            decimal parsedQty = 0;
            if (string.IsNullOrWhiteSpace(rawQty))
            {
                rowPreview.Errors.Add(new CsvRowErrorDto
                {
                    RowNumber = rowNumber,
                    Field = "quantity",
                    Problem = "Quantity is required.",
                    SuggestedCorrection = "Enter a numeric share quantity greater than zero (e.g., 100)."
                });
            }
            else if (!decimal.TryParse(rawQty, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedQty) || parsedQty <= 0)
            {
                rowPreview.Errors.Add(new CsvRowErrorDto
                {
                    RowNumber = rowNumber,
                    Field = "quantity",
                    Problem = $"Quantity '{rawQty}' must be a positive number greater than zero.",
                    SuggestedCorrection = "Provide a positive whole number greater than 0."
                });
            }
            else if (parsedQty != Math.Floor(parsedQty))
            {
                rowPreview.Errors.Add(new CsvRowErrorDto
                {
                    RowNumber = rowNumber,
                    Field = "quantity",
                    Problem = $"Fractional shares are not supported. Quantity '{rawQty}' must be a whole integer.",
                    SuggestedCorrection = "Provide a whole number of shares (e.g., 100)."
                });
            }
            else if (parsedQty > 100_000_000)
            {
                rowPreview.Errors.Add(new CsvRowErrorDto
                {
                    RowNumber = rowNumber,
                    Field = "quantity",
                    Problem = $"Quantity '{rawQty}' exceeds the maximum allowed limit of 100,000,000.",
                    SuggestedCorrection = "Check the number of shares and ensure no decimal placement error."
                });
            }
            else
            {
                qtyValid = true;
                rowPreview.Quantity = parsedQty;
            }

            // 5. Validate Price
            bool priceValid = false;
            decimal parsedPrice = 0;
            if (string.IsNullOrWhiteSpace(rawPrice))
            {
                rowPreview.Errors.Add(new CsvRowErrorDto
                {
                    RowNumber = rowNumber,
                    Field = "price_per_share",
                    Problem = "Price per share is required.",
                    SuggestedCorrection = "Enter the share price in BDT (e.g., 410.00)."
                });
            }
            else if (!decimal.TryParse(rawPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedPrice) || parsedPrice <= 0)
            {
                rowPreview.Errors.Add(new CsvRowErrorDto
                {
                    RowNumber = rowNumber,
                    Field = "price_per_share",
                    Problem = $"Price per share '{rawPrice}' must be a positive number greater than zero.",
                    SuggestedCorrection = "Provide a positive price per share in BDT."
                });
            }
            else if (parsedPrice > 100_000_000)
            {
                rowPreview.Errors.Add(new CsvRowErrorDto
                {
                    RowNumber = rowNumber,
                    Field = "price_per_share",
                    Problem = $"Price per share '{rawPrice}' exceeds the maximum allowed limit.",
                    SuggestedCorrection = "Check the price value for accuracy."
                });
            }
            else
            {
                priceValid = true;
                rowPreview.PricePerShare = parsedPrice;
            }

            if (qtyValid && priceValid)
            {
                rowPreview.TotalAmount = Math.Round(parsedQty * parsedPrice, 2);
            }

            // 6. Check Duplicate Rows within the file
            if (dateValid && matchedCompany != null && (normalizedType == "BUY" || normalizedType == "SELL") && qtyValid && priceValid)
            {
                var sig = $"{parsedDate:yyyy-MM-dd}|{matchedCompany.TickerSymbol}|{normalizedType}|{parsedQty}|{parsedPrice}";
                if (seenSignatures.TryGetValue(sig, out int firstRow))
                {
                    rowPreview.Errors.Add(new CsvRowErrorDto
                    {
                        RowNumber = rowNumber,
                        Field = "duplicate_row",
                        Problem = $"Row {rowNumber} is an exact duplicate of Row {firstRow} ({normalizedType} {parsedQty} shares of {matchedCompany.TickerSymbol} @ ৳{parsedPrice:N2} on {parsedDate:yyyy-MM-dd}).",
                        SuggestedCorrection = "Remove the duplicate row or modify its parameters if this was intended as a separate trade."
                    });
                }
                else
                {
                    seenSignatures[sig] = rowNumber;
                }
            }

            // 7. Validate SELL Availability & Running Holdings Tracking
            if (matchedCompany != null && qtyValid)
            {
                int cId = matchedCompany.CompanyId;
                var currentAvailable = runningHoldings.TryGetValue(cId, out var cur) ? cur : 0;

                if (normalizedType == "SELL")
                {
                    if (parsedQty > currentAvailable)
                    {
                        rowPreview.Errors.Add(new CsvRowErrorDto
                        {
                            RowNumber = rowNumber,
                            Field = "sell_availability",
                            Problem = $"Oversell error: Attempted to SELL {parsedQty:N2} shares of {matchedCompany.TickerSymbol}, but only {Math.Max(0, currentAvailable):N2} shares are available in portfolio holdings.",
                            SuggestedCorrection = $"Reduce SELL quantity to {Math.Max(0, currentAvailable):N2} or add a preceding BUY transaction in the CSV."
                        });
                    }
                    else
                    {
                        // Deduct from running balance
                        runningHoldings[cId] = currentAvailable - parsedQty;
                    }
                }
                else if (normalizedType == "BUY")
                {
                    // Add to running balance
                    runningHoldings[cId] = currentAvailable + parsedQty;
                }
            }

            rowPreview.IsValid = rowPreview.Errors.Count == 0;
            preview.Rows.Add(rowPreview);

            if (rowPreview.IsValid)
            {
                preview.ValidRowsCount++;
                preview.TotalEstimatedAmount += rowPreview.TotalAmount;
            }
            else
            {
                preview.InvalidRowsCount++;
                preview.Errors.AddRange(rowPreview.Errors);
            }

            rowNumber++;
        }

        preview.Summary = preview.IsValid
            ? $"All {preview.TotalRows} row(s) are valid. Ready to import (Total volume: ৳{preview.TotalEstimatedAmount:N2})."
            : $"{preview.InvalidRowsCount} of {preview.TotalRows} row(s) have validation errors. Review and correct row-level issues.";

        return preview;
    }

    private static List<List<string>> ParseCsvLines(string csvText)
    {
        var records = new List<List<string>>();
        if (string.IsNullOrEmpty(csvText)) return records;

        // Strip UTF-8 BOM if present
        if (csvText.StartsWith('\uFEFF'))
        {
            csvText = csvText[1..];
        }

        using var reader = new StringReader(csvText);
        var currentTokens = new List<string>();
        var currentToken = new StringBuilder();
        bool inQuotes = false;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            // Empty lines between data
            if (string.IsNullOrWhiteSpace(line) && !inQuotes)
            {
                continue;
            }

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // Check for escaped quote ("")
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            currentToken.Append('"');
                            i++; // Skip the next quote
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        currentToken.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',' || c == ';') // Comma or semicolon separator
                    {
                        currentTokens.Add(currentToken.ToString().Trim());
                        currentToken.Clear();
                    }
                    else
                    {
                        currentToken.Append(c);
                    }
                }
            }

            if (!inQuotes)
            {
                currentTokens.Add(currentToken.ToString().Trim());
                currentToken.Clear();

                // Only add if not an entirely blank row
                if (currentTokens.Any(t => !string.IsNullOrWhiteSpace(t)))
                {
                    records.Add(currentTokens);
                }
                currentTokens = new List<string>();
            }
            else
            {
                // Multi-line quoted field: preserve newline
                currentToken.Append('\n');
            }
        }

        // Handle case where file ended inside quotes or with leftover token
        if (currentToken.Length > 0 || inQuotes)
        {
            currentTokens.Add(currentToken.ToString().Trim());
            records.Add(currentTokens);
        }

        return records;
    }

    private static Dictionary<string, int> MapHeaders(List<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headers.Count; i++)
        {
            var raw = headers[i].Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");

            if (raw is "transactiondate" or "date" or "txdate" or "tradedate")
            {
                map["date"] = i;
            }
            else if (raw is "companyticker" or "ticker" or "symbol" or "company" or "stock" or "tickersymbol")
            {
                map["ticker"] = i;
            }
            else if (raw is "transactiontype" or "type" or "action" or "side" or "buyorsell")
            {
                map["type"] = i;
            }
            else if (raw is "quantity" or "qty" or "shares" or "sharecount" or "numshares")
            {
                map["quantity"] = i;
            }
            else if (raw is "pricepershare" or "price" or "shareprice" or "rate" or "unitprice")
            {
                map["price"] = i;
            }
            else if (raw is "notes" or "note" or "memo" or "description" or "remarks")
            {
                map["notes"] = i;
            }
        }

        return map;
    }

    private static List<string> GetMissingRequiredHeaders(Dictionary<string, int> headerMap)
    {
        var required = new[] { "date", "ticker", "type", "quantity", "price" };
        var missing = new List<string>();

        foreach (var req in required)
        {
            if (!headerMap.ContainsKey(req))
            {
                missing.Add(req switch
                {
                    "date" => "transaction_date",
                    "ticker" => "company_ticker",
                    "type" => "transaction_type",
                    "quantity" => "quantity",
                    "price" => "price_per_share",
                    _ => req
                });
            }
        }

        return missing;
    }

    private static string GetColumnValue(List<string> tokens, Dictionary<string, int> headerMap, string columnKey)
    {
        if (headerMap.TryGetValue(columnKey, out int index) && index >= 0 && index < tokens.Count)
        {
            return tokens[index].Trim();
        }
        return string.Empty;
    }

    private static bool TryParseDate(string raw, out DateTime date)
    {
        var formats = new[]
        {
            "yyyy-MM-dd",
            "yyyy/MM/dd",
            "dd-MM-yyyy",
            "dd/MM/yyyy",
            "MM-dd-yyyy",
            "MM/dd/yyyy",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-dd HH:mm:ss",
            "yyyyMMdd"
        };

        if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static string SanitizeFormulaInjection(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // If the cell begins with formula characters, neutralize it
        var trimmed = input.TrimStart();
        if (trimmed.StartsWith('=') || trimmed.StartsWith('+') || trimmed.StartsWith('@') || trimmed.StartsWith('\t') || trimmed.StartsWith('\r'))
        {
            return "'" + input;
        }

        return input;
    }
}
