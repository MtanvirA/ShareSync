using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShareSync.Application.Common.Helpers;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.MarketData;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Infrastructure.Services;

/// <summary>
/// FEATURE: DSE Market Data
/// DSE: Price Synchronization & Market Data Provider (LF-31)
/// 
/// DSE: Live Price Retrieval
/// Fetches real-time Dhaka Stock Exchange market data from the StockChartBD API endpoint.
/// Includes automatic HTTP retry logic with backoff, User-Agent identification, and JSON payload deserialization.
/// 
/// DSE: Price Synchronization
/// Synchronizes latest market quotes into the MySQL `companies` table and daily OHLC snapshots into `company_price_history`.
/// 
/// DATA QUALITY: Plausibility Reconciliation & Anomaly Guard
/// Compares incoming quotes against existing prices. Rejects extreme flash anomalies (>10x or &lt;0.1x deviations)
/// to safeguard portfolio valuations from upstream corrupt market data spikes.
/// 
/// DATA QUALITY: Duplicate Handling & Idempotency
/// Intraday synchronizations update existing records for the same trading day rather than inserting duplicates.
/// 
/// PERFORMANCE: Throttling & Politeness Delay
/// Introduces 30ms polite request delays between ticker queries to prevent rate-limiting external market data endpoints.
/// 
/// DATABASE: Table — companies, company_price_history
/// </summary>
public class DsePriceService : IDsePriceService
{
    private readonly IApplicationDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly IStockChartBdClient? _stockChartClient;
    private readonly IAlertService? _alertService;
    private readonly ILogger<DsePriceService> _logger;

    public DsePriceService(
        IApplicationDbContext context,
        HttpClient httpClient,
        ILogger<DsePriceService> logger)
        : this(context, httpClient, null, null, logger)
    {
    }

    public DsePriceService(
        IApplicationDbContext context,
        HttpClient httpClient,
        IAlertService? alertService,
        ILogger<DsePriceService> logger)
        : this(context, httpClient, null, alertService, logger)
    {
    }

    [ActivatorUtilitiesConstructor]
    public DsePriceService(
        IApplicationDbContext context,
        HttpClient httpClient,
        IStockChartBdClient? stockChartClient,
        IAlertService? alertService,
        ILogger<DsePriceService> logger)
    {
        _context = context;
        _httpClient = httpClient;
        _stockChartClient = stockChartClient;
        _alertService = alertService;
        _logger = logger;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ShareSync-PortfolioTracker/1.0 (+https://github.com/MtanvirA/ShareSync)");
        }
    }

    public async Task<decimal?> FetchLatestPriceAsync(string tickerSymbol, CancellationToken cancellationToken = default)
    {
        var quote = await FetchQuoteDetailsAsync(tickerSymbol, cancellationToken);
        return quote?.Price;
    }

    public async Task<DseQuoteData?> FetchQuoteDetailsAsync(string tickerSymbol, CancellationToken cancellationToken = default)
    {
        var normalizedTicker = MarketDataPathResolver.NormalizeTicker(tickerSymbol);
        if (string.IsNullOrWhiteSpace(normalizedTicker))
        {
            return null;
        }

        if (_stockChartClient != null)
        {
            var stockChartQuote = await _stockChartClient.GetQuoteAsync(normalizedTicker, cancellationToken);
            if (stockChartQuote != null)
            {
                return stockChartQuote;
            }
        }

        var url = $"https://stockchartbd.com/api/v1.php?endpoint=quote&symbol={Uri.EscapeDataString(normalizedTicker)}";
        const int maxAttempts = 2;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("DSE quote fetch for {Ticker} returned HTTP {StatusCode} (Attempt {Attempt}/{Max})",
                        normalizedTicker, response.StatusCode, attempt, maxAttempts);

                    if (attempt < maxAttempts && (int)response.StatusCode >= 500)
                    {
                        await Task.Delay(300, cancellationToken);
                        continue;
                    }
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("ok", out var okEl) && okEl.GetBoolean() &&
                    root.TryGetProperty("quote", out var quoteEl))
                {
                    decimal price = 0;
                    decimal? openPrice = null;
                    decimal? highPrice = null;
                    decimal? lowPrice = null;
                    long? volume = null;

                    if (quoteEl.TryGetProperty("close", out var closeEl) && closeEl.TryGetDecimal(out var parsedClose))
                    {
                        price = parsedClose;
                    }

                    if (quoteEl.TryGetProperty("open", out var openEl) && openEl.TryGetDecimal(out var parsedOpen))
                    {
                        openPrice = parsedOpen;
                        if (price <= 0) price = parsedOpen;
                    }

                    if (quoteEl.TryGetProperty("high", out var highEl) && highEl.TryGetDecimal(out var parsedHigh))
                    {
                        highPrice = parsedHigh;
                    }

                    if (quoteEl.TryGetProperty("low", out var lowEl) && lowEl.TryGetDecimal(out var parsedLow))
                    {
                        lowPrice = parsedLow;
                    }

                    if (quoteEl.TryGetProperty("volume", out var volEl) && volEl.TryGetInt64(out var parsedVol) && parsedVol >= 0)
                    {
                        volume = parsedVol;
                    }

                    DateTime? quoteTimestamp = null;
                    if (quoteEl.TryGetProperty("time", out var timeEl) || quoteEl.TryGetProperty("timestamp", out timeEl) || quoteEl.TryGetProperty("date", out timeEl) || quoteEl.TryGetProperty("updated_at", out timeEl))
                    {
                        if (timeEl.ValueKind == JsonValueKind.String && DateTime.TryParse(timeEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDt))
                        {
                            quoteTimestamp = parsedDt;
                        }
                        else if (timeEl.ValueKind == JsonValueKind.Number && timeEl.TryGetInt64(out var unixSec))
                        {
                            quoteTimestamp = DateTimeOffset.FromUnixTimeSeconds(unixSec).UtcDateTime;
                        }
                    }
                    else if (root.TryGetProperty("timestamp", out var rootTimeEl) || root.TryGetProperty("time", out rootTimeEl) || root.TryGetProperty("date", out rootTimeEl))
                    {
                        if (rootTimeEl.ValueKind == JsonValueKind.String && DateTime.TryParse(rootTimeEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedRootDt))
                        {
                            quoteTimestamp = parsedRootDt;
                        }
                        else if (rootTimeEl.ValueKind == JsonValueKind.Number && rootTimeEl.TryGetInt64(out var unixSec))
                        {
                            quoteTimestamp = DateTimeOffset.FromUnixTimeSeconds(unixSec).UtcDateTime;
                        }
                    }

                    // Validate timestamp plausibility (reject future timestamps, detect stale)
                    if (!MarketDataPathResolver.ValidateQuoteTimestamp(quoteTimestamp, DateTime.UtcNow, out string quoteStatus, out string? tsError))
                    {
                        _logger.LogWarning("DSE quote for {Ticker} rejected due to timestamp anomaly: {Error}", normalizedTicker, tsError);
                        return null;
                    }

                    // Validate price plausibility and harmonize OHLC
                    if (MarketDataPathResolver.ValidateAndHarmonizeOhlc(price, ref openPrice, ref highPrice, ref lowPrice))
                    {
                        return new DseQuoteData
                        {
                            Price = price,
                            OpenPrice = openPrice,
                            HighPrice = highPrice,
                            LowPrice = lowPrice,
                            Volume = volume,
                            QuoteTimestamp = quoteTimestamp,
                            Source = "StockChartBD Live API",
                            Status = quoteStatus,
                            ValidationState = "VALID"
                        };
                    }
                    else
                    {
                        _logger.LogWarning("DSE quote for {Ticker} failed sanity checks: Close={Price}", normalizedTicker, price);
                    }
                }
                return null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve live DSE quote for ticker {Ticker} (Attempt {Attempt}/{Max}): {Message}",
                    normalizedTicker, attempt, maxAttempts, ex.Message);

                if (attempt < maxAttempts)
                {
                    await Task.Delay(350, cancellationToken);
                }
            }
        }

        return null;
    }

    public async Task<ApiResponse<DsePriceUpdateResultDto>> SyncAllCompanyPricesAsync(CancellationToken cancellationToken = default)
    {
        var companies = await _context.Companies.ToListAsync(cancellationToken);
        var syncTime = DateTime.UtcNow;
        var todayDate = syncTime.Date;
        var result = new DsePriceUpdateResultDto
        {
            SyncedAt = syncTime
        };

        if (companies.Count == 0)
        {
            return ApiResponse<DsePriceUpdateResultDto>.Ok(result, "No companies found in database to update.");
        }

        int updatedCount = 0;
        int historyCount = 0;

        foreach (var company in companies)
        {
            try
            {
                // Politeness delay to prevent rate limiting external DSE service
                await Task.Delay(30, cancellationToken);

                var quote = await FetchQuoteDetailsAsync(company.TickerSymbol, cancellationToken);
                if (quote != null && MarketDataPathResolver.IsValidPrice(quote.Price))
                {
                    // Plausibility reconciliation & deviation guard: protect against corrupt 100x or 0.01x flash anomalies
                    if (company.CurrentPrice > 0 && !MarketDataPathResolver.ReconcilePriceMovement(company.CurrentPrice, quote.Price, out var reconResult, out var reconMsg))
                    {
                        _logger.LogWarning(
                            "Reconciliation rejected for {Ticker}: {Message}. Preserving existing price {OldPrice:C}.",
                            company.TickerSymbol, reconMsg, company.CurrentPrice);
                        result.UpdatedPrices[company.TickerSymbol] = company.CurrentPrice;
                        result.PriceProvenance[company.TickerSymbol] = "REJECTED_ANOMALY";
                        continue;
                    }

                    // 1. Update current price if changed
                    if (company.CurrentPrice != quote.Price)
                    {
                        _logger.LogInformation(
                            "Updating current price for {Ticker}: {OldPrice:C} -> {NewPrice:C}",
                            company.TickerSymbol, company.CurrentPrice, quote.Price);

                        company.CurrentPrice = quote.Price;
                        updatedCount++;
                    }

                    result.UpdatedPrices[company.TickerSymbol] = quote.Price;
                    result.PriceProvenance[company.TickerSymbol] = quote.Status == "STALE" ? "STALE" : "LIVE";

                    // 2. Canonical Daily Upsert & Idempotency:
                    // Check if a price history record already exists for this company on today's trading date or within recent window
                    var recentWindow = syncTime.AddMinutes(-10);
                    var existingToday = await _context.CompanyPriceHistories
                        .FirstOrDefaultAsync(h => h.CompanyId == company.CompanyId &&
                            (h.TradingDate == todayDate || (h.TradingDate == null && h.RecordedAt > recentWindow)),
                            cancellationToken);

                    string feedDataset = quote.Status == "STALE" ? "DSE Delayed/Stale Feed" : "DSE Real-Time Market Feed";

                    if (existingToday != null)
                    {
                        // Update existing intraday record without creating duplicate
                        existingToday.Price = quote.Price;
                        if (quote.OpenPrice.HasValue && (!existingToday.OpenPrice.HasValue || existingToday.OpenPrice <= 0))
                            existingToday.OpenPrice = quote.OpenPrice;
                        if (quote.HighPrice.HasValue)
                            existingToday.HighPrice = Math.Max(existingToday.HighPrice ?? quote.Price, quote.HighPrice.Value);
                        if (quote.LowPrice.HasValue)
                            existingToday.LowPrice = Math.Min(existingToday.LowPrice ?? quote.Price, quote.LowPrice.Value);
                        if (quote.Volume.HasValue)
                            existingToday.Volume = quote.Volume;
                        existingToday.RecordedAt = syncTime;
                        existingToday.Source = quote.Source;
                        existingToday.SourceDataset = feedDataset;
                    }
                    else
                    {
                        var history = new CompanyPriceHistory
                        {
                            CompanyId = company.CompanyId,
                            TradingDate = todayDate,
                            Price = quote.Price,
                            OpenPrice = quote.OpenPrice,
                            HighPrice = quote.HighPrice,
                            LowPrice = quote.LowPrice,
                            Volume = quote.Volume,
                            RecordedAt = syncTime,
                            Source = quote.Source,
                            SourceDataset = feedDataset,
                            ImportBatchId = $"SYNC_{syncTime:yyyyMMddHHmmss}",
                            CreatedAt = syncTime
                        };

                        _context.CompanyPriceHistories.Add(history);
                        historyCount++;
                    }
                }
                else
                {
                    result.UpdatedPrices[company.TickerSymbol] = company.CurrentPrice;
                    result.PriceProvenance[company.TickerSymbol] = "PRESERVED_FALLBACK";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failure processing price sync for company {Ticker}. Preserving current price.", company.TickerSymbol);
                result.UpdatedPrices[company.TickerSymbol] = company.CurrentPrice;
                result.PriceProvenance[company.TickerSymbol] = "PRESERVED_FALLBACK";
            }
        }

        if (updatedCount > 0 || historyCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);

            // Evaluate price-based alerts if prices were synchronized
            if (_alertService != null)
            {
                try
                {
                    int triggeredAlerts = await _alertService.EvaluatePriceAlertsAsync(cancellationToken);
                    if (triggeredAlerts > 0)
                    {
                        _logger.LogInformation("Evaluated price alerts after DSE price sync: {Count} alerts triggered", triggeredAlerts);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to evaluate price alerts after DSE sync");
                }
            }
        }

        result.UpdatedCount = updatedCount;
        result.HistoryCount = historyCount;

        return ApiResponse<DsePriceUpdateResultDto>.Ok(
            result,
            $"Successfully synchronized DSE prices for {companies.Count} companies ({updatedCount} prices updated, {historyCount} history snapshots recorded).");
    }

    private static List<DseListedCompanyDto>? _cachedDseCompanies;
    private static DateTime _dseCompaniesCacheExpiry = DateTime.MinValue;
    private static readonly SemaphoreSlim _cacheLock = new(1, 1);

    public static void ResetCaches()
    {
        _cachedDseCompanies = null;
        _dseCompaniesCacheExpiry = DateTime.MinValue;
    }

    public async Task<List<DseListedCompanyDto>> GetDseListedCompaniesAsync(CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedDseCompanies == null || DateTime.UtcNow >= _dseCompaniesCacheExpiry)
            {
                List<DseListedCompanyDto>? fetched = null;
                if (_stockChartClient != null)
                {
                    fetched = await _stockChartClient.GetCompaniesAsync(forceRefresh: false, cancellationToken);
                }

                if (fetched == null || fetched.Count == 0)
                {
                    fetched = await FetchAllDseCompaniesFromApiAsync(cancellationToken);
                }

                if (fetched != null && fetched.Count > 0)
                {
                    _cachedDseCompanies = fetched;
                    _dseCompaniesCacheExpiry = DateTime.UtcNow.AddMinutes(30);
                }
                else if (_cachedDseCompanies == null)
                {
                    _cachedDseCompanies = new List<DseListedCompanyDto>();
                }
            }
        }
        finally
        {
            _cacheLock.Release();
        }

        // Cross-reference with current database companies to mark IsAdded status
        var localCompanies = await _context.Companies
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var localMap = localCompanies.ToDictionary(c => c.TickerSymbol.ToUpperInvariant(), c => c);

        var resultList = new List<DseListedCompanyDto>();
        var seenSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_cachedDseCompanies != null)
        {
            foreach (var item in _cachedDseCompanies)
            {
                seenSymbols.Add(item.Symbol);
                localMap.TryGetValue(item.Symbol.ToUpperInvariant(), out var local);

                resultList.Add(new DseListedCompanyDto
                {
                    Symbol = item.Symbol,
                    Name = item.Name,
                    Sector = item.Sector,
                    Category = item.Category,
                    MarketCapMn = item.MarketCapMn,
                    IsAdded = local != null,
                    LocalCompanyId = local?.CompanyId,
                    LocalPrice = local?.CurrentPrice
                });
            }
        }

        // Also ensure any local companies not in the external list are included
        foreach (var local in localCompanies)
        {
            if (!seenSymbols.Contains(local.TickerSymbol))
            {
                resultList.Add(new DseListedCompanyDto
                {
                    Symbol = local.TickerSymbol,
                    Name = local.CompanyName,
                    Sector = "General",
                    IsAdded = true,
                    LocalCompanyId = local.CompanyId,
                    LocalPrice = local.CurrentPrice
                });
            }
        }

        return resultList.OrderBy(c => c.Symbol).ToList();
    }

    private async Task<List<DseListedCompanyDto>> FetchAllDseCompaniesFromApiAsync(CancellationToken cancellationToken)
    {
        var list = new List<DseListedCompanyDto>();
        try
        {
            var url = "https://stockchartbd.com/api/v1.php?endpoint=companies";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("FetchAllDseCompanies returned status code: {StatusCode}", response.StatusCode);
                return list;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("companies", out var compsEl) && compsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in compsEl.EnumerateArray())
                {
                    var sym = el.TryGetProperty("symbol", out var sEl) ? sEl.GetString() : null;
                    if (string.IsNullOrWhiteSpace(sym)) continue;

                    var name = el.TryGetProperty("name", out var nEl) ? nEl.GetString() ?? sym : sym;
                    var sector = el.TryGetProperty("sector", out var secEl) ? secEl.GetString() ?? "Other" : "Other";
                    var category = el.TryGetProperty("category", out var catEl) ? catEl.GetString() : null;
                    decimal? mcap = null;
                    if (el.TryGetProperty("market_cap_mn", out var mEl) && mEl.ValueKind != JsonValueKind.Null && mEl.TryGetDecimal(out var parsedMcap))
                    {
                        mcap = parsedMcap;
                    }

                    list.Add(new DseListedCompanyDto
                    {
                        Symbol = sym.Trim().ToUpperInvariant(),
                        Name = name.Trim(),
                        Sector = sector.Trim(),
                        Category = category?.Trim(),
                        MarketCapMn = mcap
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch companies from stockchartbd API: {Message}", ex.Message);
        }

        return list;
    }

    public async Task<Application.DTOs.Companies.CompanyDto> ImportCompanyFromDseAsync(
        string symbol,
        string? preferredName = null,
        string? preferredSector = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new Application.Common.Exceptions.AppException("Ticker symbol is required.", 400);
        }

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();

        // 1. Check if already exists in database
        var existing = await _context.Companies
            .Include(c => c.Sector)
            .FirstOrDefaultAsync(c => c.TickerSymbol == normalizedSymbol, cancellationToken);

        if (existing != null)
        {
            existing.IsActive = true;
            await _context.SaveChangesAsync(cancellationToken);

            return new Application.DTOs.Companies.CompanyDto
            {
                CompanyId = existing.CompanyId,
                CompanyName = existing.CompanyName,
                TickerSymbol = existing.TickerSymbol,
                SectorId = existing.SectorId,
                SectorName = existing.Sector?.SectorName ?? "General",
                CurrentPrice = existing.CurrentPrice,
                MarketCap = existing.MarketCap
            };
        }

        // 2. Fetch listed company reference details
        var allDse = await GetDseListedCompaniesAsync(cancellationToken);
        var dseRef = allDse.FirstOrDefault(c => string.Equals(c.Symbol, normalizedSymbol, StringComparison.OrdinalIgnoreCase));

        var compName = !string.IsNullOrWhiteSpace(preferredName)
            ? preferredName.Trim()
            : (!string.IsNullOrWhiteSpace(dseRef?.Name) ? dseRef.Name : normalizedSymbol);

        var sectorName = !string.IsNullOrWhiteSpace(preferredSector)
            ? preferredSector.Trim()
            : (!string.IsNullOrWhiteSpace(dseRef?.Sector) ? dseRef.Sector : "Other");

        // 3. Normalize or map sector
        var sector = await _context.Sectors
            .FirstOrDefaultAsync(s => s.SectorName.ToUpper() == sectorName.ToUpper(), cancellationToken);

        if (sector == null)
        {
            sector = new Sector
            {
                SectorName = sectorName,
                Description = $"{sectorName} companies"
            };
            _context.Sectors.Add(sector);
            await _context.SaveChangesAsync(cancellationToken);
        }

        // 4. Fetch live market quote for price
        var quote = await FetchQuoteDetailsAsync(normalizedSymbol, cancellationToken);
        decimal initialPrice = quote?.Price ?? (quote?.OpenPrice ?? 100.00m);
        if (initialPrice <= 0) initialPrice = 100.00m;

        decimal? marketCap = dseRef?.MarketCapMn.HasValue == true
            ? dseRef.MarketCapMn.Value * 1000000m
            : null;

        var newCompany = new Company
        {
            TickerSymbol = normalizedSymbol,
            CompanyName = compName,
            SectorId = sector.SectorId,
            CurrentPrice = initialPrice,
            MarketCap = marketCap,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Companies.Add(newCompany);
        await _context.SaveChangesAsync(cancellationToken);

        // 5. Add initial Price History record
        var historyPoint = new CompanyPriceHistory
        {
            CompanyId = newCompany.CompanyId,
            Price = newCompany.CurrentPrice,
            OpenPrice = quote?.OpenPrice ?? newCompany.CurrentPrice,
            HighPrice = quote?.HighPrice ?? newCompany.CurrentPrice,
            LowPrice = quote?.LowPrice ?? newCompany.CurrentPrice,
            Volume = quote?.Volume ?? 35000,
            TradingDate = DateTime.UtcNow.Date,
            RecordedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        _context.CompanyPriceHistories.Add(historyPoint);
        await _context.SaveChangesAsync(cancellationToken);

        // 6. Attempt to populate historical price records from DSE_Data.csv with canonical path & date resolution
        try
        {
            var csvPath = MarketDataPathResolver.LocateDseCsv();
            if (!string.IsNullOrWhiteSpace(csvPath) && File.Exists(csvPath))
            {
                var existingDatesList = await _context.CompanyPriceHistories
                    .Where(h => h.CompanyId == newCompany.CompanyId && h.TradingDate != null)
                    .Select(h => h.TradingDate!.Value)
                    .ToListAsync(cancellationToken);
                var existingDates = existingDatesList.ToHashSet();

                var dedup = new Dictionary<string, CompanyPriceHistory>();
                using var reader = new StreamReader(csvPath);
                string? header = await reader.ReadLineAsync(cancellationToken);

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(',');
                    if (parts.Length >= 7 && string.Equals(parts[0].Trim(), normalizedSymbol, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!MarketDataPathResolver.TryParseTradingDate(parts[1], out DateTime validDate))
                            continue;

                        if (existingDates.Contains(validDate.Date))
                            continue;

                        if (!decimal.TryParse(parts[5].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal cl) || !MarketDataPathResolver.IsValidPrice(cl))
                            continue;

                        decimal? op = decimal.TryParse(parts[2].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal o) && o > 0 ? o : cl;
                        decimal? hi = decimal.TryParse(parts[3].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal h) && h > 0 ? h : Math.Max(op ?? cl, cl);
                        decimal? lo = decimal.TryParse(parts[4].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal l) && l > 0 ? l : Math.Min(op ?? cl, cl);

                        if (!MarketDataPathResolver.ValidateAndHarmonizeOhlc(cl, ref op, ref hi, ref lo))
                            continue;

                        long? vol = null;
                        if (long.TryParse(parts[6].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out long v) && v >= 0)
                            vol = v;

                        var key = validDate.ToString("yyyy-MM-dd");
                        dedup[key] = new CompanyPriceHistory
                        {
                            CompanyId = newCompany.CompanyId,
                            TradingDate = validDate.Date,
                            RecordedAt = validDate,
                            Price = cl,
                            OpenPrice = op,
                            HighPrice = hi,
                            LowPrice = lo,
                            Volume = vol,
                            Source = "Harvard Dataverse",
                            SourceDataset = "Dhaka Stock Exchange Historical Data (1999-2025)",
                            SourceDoi = "10.7910/DVN/XIFYT1",
                            ImportBatchId = $"CSV_IMPORT_{DateTime.UtcNow:yyyyMMdd}",
                            CreatedAt = DateTime.UtcNow
                        };
                    }
                }

                if (dedup.Any())
                {
                    await _context.CompanyPriceHistories.AddRangeAsync(dedup.Values, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Loaded {Count} historical price records for {Ticker} from canonical CSV ({Path})",
                        dedup.Count, normalizedSymbol, csvPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not auto-seed historical prices from CSV for {Ticker}", normalizedSymbol);
        }

        _logger.LogInformation("Successfully imported DSE company {Ticker} ({Name}) at price {Price:C}", newCompany.TickerSymbol, newCompany.CompanyName, newCompany.CurrentPrice);

        return new Application.DTOs.Companies.CompanyDto
        {
            CompanyId = newCompany.CompanyId,
            CompanyName = newCompany.CompanyName,
            TickerSymbol = newCompany.TickerSymbol,
            SectorId = newCompany.SectorId,
            SectorName = sector.SectorName,
            CurrentPrice = newCompany.CurrentPrice,
            MarketCap = newCompany.MarketCap
        };
    }

    public async Task<StockChartBdMarketSummaryDto?> GetMarketSummaryAsync(CancellationToken cancellationToken = default)
    {
        StockChartBdMarketSummaryDto? summary = null;
        if (_stockChartClient != null)
        {
            summary = await _stockChartClient.GetMarketSummaryAsync(forceRefresh: false, cancellationToken);
        }

        if (summary == null)
        {
            return null;
        }

        if (summary.Indices != null && summary.Indices.Count > 0)
        {
            try
            {
                foreach (var (indexCode, indexDto) in summary.Indices)
                {
                    if (string.IsNullOrWhiteSpace(indexCode) || indexDto == null || indexDto.Close <= 0)
                    {
                        continue;
                    }

                    var code = indexCode.Trim().ToUpperInvariant();
                    DateTime tradingDate = DateTime.UtcNow.Date;
                    if (!string.IsNullOrWhiteSpace(indexDto.Date) &&
                        DateTime.TryParse(indexDto.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                    {
                        tradingDate = parsedDate.Date;
                    }

                    var existing = await _context.BenchmarkPriceHistories
                        .FirstOrDefaultAsync(b => b.BenchmarkCode == code && b.TradingDate == tradingDate, cancellationToken);

                    if (existing != null)
                    {
                        existing.CloseValue = indexDto.Close;
                    }
                    else
                    {
                        string benchmarkName = code switch
                        {
                            "DSEX" => "DSE Broad Index (DSEX)",
                            "DS30" => "DSE 30 Index (DS30)",
                            "DSES" => "DSE Shariah Index (DSES)",
                            "CDSET" => "CSE-DSE Shariah Total Return (CDSET)",
                            _ => $"{code} Index"
                        };

                        _context.BenchmarkPriceHistories.Add(new BenchmarkPriceHistory
                        {
                            BenchmarkCode = code,
                            BenchmarkName = benchmarkName,
                            TradingDate = tradingDate,
                            CloseValue = indexDto.Close,
                            Source = "StockChartBD Live API",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist benchmark indices from market summary: {Message}", ex.Message);
            }
        }

        return summary;
    }

    public async Task<ApiResponse<int>> SyncHistoricalPricesAsync(string tickerSymbol, int limit = 100, CancellationToken cancellationToken = default)
    {
        var normalizedTicker = MarketDataPathResolver.NormalizeTicker(tickerSymbol);
        if (string.IsNullOrWhiteSpace(normalizedTicker))
        {
            return ApiResponse<int>.Fail("Ticker symbol is required.");
        }

        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.TickerSymbol.ToUpper() == normalizedTicker.ToUpper(), cancellationToken);

        if (company == null)
        {
            return ApiResponse<int>.Fail($"Company with ticker '{tickerSymbol}' was not found in the database.");
        }

        if (_stockChartClient == null)
        {
            return ApiResponse<int>.Fail("StockChartBD client is not available.");
        }

        var history = await _stockChartClient.GetHistoricalPricesAsync(normalizedTicker, limit, cancellationToken);
        if (history == null || history.Count == 0)
        {
            return ApiResponse<int>.Ok(0, $"No historical price records returned for {normalizedTicker}.");
        }

        var existingDatesList = await _context.CompanyPriceHistories
            .Where(h => h.CompanyId == company.CompanyId && h.TradingDate != null)
            .Select(h => h.TradingDate!.Value.Date)
            .ToListAsync(cancellationToken);

        var existingDates = existingDatesList.ToHashSet();
        var toAdd = new List<CompanyPriceHistory>();

        foreach (var item in history)
        {
            if (string.IsNullOrWhiteSpace(item.Date)) continue;

            if (!MarketDataPathResolver.TryParseTradingDate(item.Date, out DateTime validDate))
            {
                if (!DateTime.TryParse(item.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out validDate))
                {
                    continue;
                }
            }

            if (existingDates.Contains(validDate.Date))
            {
                continue;
            }

            decimal cl = item.Close;
            if (!MarketDataPathResolver.IsValidPrice(cl))
            {
                continue;
            }

            decimal? op = item.Open > 0 ? item.Open : cl;
            decimal? hi = item.High > 0 ? item.High : Math.Max(op ?? cl, cl);
            decimal? lo = item.Low > 0 ? item.Low : Math.Min(op ?? cl, cl);

            if (!MarketDataPathResolver.ValidateAndHarmonizeOhlc(cl, ref op, ref hi, ref lo))
            {
                continue;
            }

            existingDates.Add(validDate.Date);
            toAdd.Add(new CompanyPriceHistory
            {
                CompanyId = company.CompanyId,
                TradingDate = validDate.Date,
                RecordedAt = validDate,
                Price = cl,
                OpenPrice = op,
                HighPrice = hi,
                LowPrice = lo,
                Volume = item.Volume,
                Source = "StockChartBD History API",
                SourceDataset = "StockChartBD Historical Price Feed",
                ImportBatchId = $"SYNC_HIST_{DateTime.UtcNow:yyyyMMdd}",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (toAdd.Count > 0)
        {
            await _context.CompanyPriceHistories.AddRangeAsync(toAdd, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Successfully persisted {Count} historical price records for {Ticker}.", toAdd.Count, normalizedTicker);
        }

        return ApiResponse<int>.Ok(toAdd.Count, $"Successfully synchronized {toAdd.Count} historical price records for {company.TickerSymbol}.");
    }

    public async Task<StockChartBdFinancialsDto?> GetCompanyFinancialsAsync(string tickerSymbol, CancellationToken cancellationToken = default)
    {
        if (_stockChartClient == null)
        {
            return null;
        }

        return await _stockChartClient.GetFinancialsAsync(tickerSymbol, cancellationToken);
    }

    public async Task<StockChartBdHealthDto?> GetCompanyHealthAsync(string tickerSymbol, CancellationToken cancellationToken = default)
    {
        if (_stockChartClient == null)
        {
            return null;
        }

        return await _stockChartClient.GetHealthAsync(tickerSymbol, cancellationToken);
    }

    public Task<StockChartBdDiagnosticsDto> GetApiDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        if (_stockChartClient != null)
        {
            return Task.FromResult(_stockChartClient.GetDiagnostics());
        }

        return Task.FromResult(new StockChartBdDiagnosticsDto());
    }
}

