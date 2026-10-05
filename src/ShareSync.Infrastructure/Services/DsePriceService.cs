using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Infrastructure.Services;

public class DsePriceService : IDsePriceService
{
    private readonly IApplicationDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly IAlertService? _alertService;
    private readonly ILogger<DsePriceService> _logger;

    public DsePriceService(
        IApplicationDbContext context,
        HttpClient httpClient,
        ILogger<DsePriceService> logger)
        : this(context, httpClient, null, logger)
    {
    }

    [ActivatorUtilitiesConstructor]
    public DsePriceService(
        IApplicationDbContext context,
        HttpClient httpClient,
        IAlertService? alertService,
        ILogger<DsePriceService> logger)
    {
        _context = context;
        _httpClient = httpClient;
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
        if (string.IsNullOrWhiteSpace(tickerSymbol))
        {
            return null;
        }

        var normalizedTicker = tickerSymbol.Trim().ToUpperInvariant();
        var url = $"https://stockchartbd.com/api/v1.php?endpoint=quote&symbol={Uri.EscapeDataString(normalizedTicker)}";

        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("DSE quote fetch for {Ticker} returned HTTP {StatusCode}", normalizedTicker, response.StatusCode);
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

                if (quoteEl.TryGetProperty("close", out var closeEl) && closeEl.TryGetDecimal(out var parsedClose) && parsedClose > 0)
                {
                    price = parsedClose;
                }

                if (quoteEl.TryGetProperty("open", out var openEl) && openEl.TryGetDecimal(out var parsedOpen) && parsedOpen > 0)
                {
                    openPrice = parsedOpen;
                    if (price <= 0) price = parsedOpen;
                }

                if (quoteEl.TryGetProperty("high", out var highEl) && highEl.TryGetDecimal(out var parsedHigh) && parsedHigh > 0)
                {
                    highPrice = parsedHigh;
                }

                if (quoteEl.TryGetProperty("low", out var lowEl) && lowEl.TryGetDecimal(out var parsedLow) && parsedLow > 0)
                {
                    lowPrice = parsedLow;
                }

                if (quoteEl.TryGetProperty("volume", out var volEl) && volEl.TryGetInt64(out var parsedVol) && parsedVol >= 0)
                {
                    volume = parsedVol;
                }

                if (price > 0)
                {
                    return new DseQuoteData
                    {
                        Price = price,
                        OpenPrice = openPrice,
                        HighPrice = highPrice,
                        LowPrice = lowPrice,
                        Volume = volume
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve live DSE quote for ticker {Ticker}: {Message}", normalizedTicker, ex.Message);
        }

        return null;
    }

    public async Task<ApiResponse<DsePriceUpdateResultDto>> SyncAllCompanyPricesAsync(CancellationToken cancellationToken = default)
    {
        var companies = await _context.Companies.ToListAsync(cancellationToken);
        var syncTime = DateTime.UtcNow;
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
                var quote = await FetchQuoteDetailsAsync(company.TickerSymbol, cancellationToken);
                if (quote != null && quote.Price > 0)
                {
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

                    // 2. Duplicate Prevention Strategy:
                    // Check if an identical price history snapshot was already recorded for this company
                    // within the 10-minute synchronization interval.
                    var latestHistory = await _context.CompanyPriceHistories
                        .Where(h => h.CompanyId == company.CompanyId)
                        .OrderByDescending(h => h.RecordedAt)
                        .FirstOrDefaultAsync(cancellationToken);

                    bool isDuplicate = latestHistory != null &&
                                       latestHistory.Price == quote.Price &&
                                       (syncTime - latestHistory.RecordedAt) < TimeSpan.FromMinutes(10);

                    if (!isDuplicate)
                    {
                        var history = new CompanyPriceHistory
                        {
                            CompanyId = company.CompanyId,
                            Price = quote.Price,
                            OpenPrice = quote.OpenPrice,
                            HighPrice = quote.HighPrice,
                            LowPrice = quote.LowPrice,
                            Volume = quote.Volume,
                            RecordedAt = syncTime,
                            CreatedAt = syncTime
                        };

                        _context.CompanyPriceHistories.Add(history);
                        historyCount++;
                    }
                }
                else
                {
                    result.UpdatedPrices[company.TickerSymbol] = company.CurrentPrice;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failure processing price sync for company {Ticker}. Preserving current price.", company.TickerSymbol);
                result.UpdatedPrices[company.TickerSymbol] = company.CurrentPrice;
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

    public async Task<List<DseListedCompanyDto>> GetDseListedCompaniesAsync(CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedDseCompanies == null || DateTime.UtcNow >= _dseCompaniesCacheExpiry)
            {
                var fetched = await FetchAllDseCompaniesFromApiAsync(cancellationToken);
                if (fetched.Count > 0)
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

            if (root.TryGetProperty("ok", out var okEl) && okEl.GetBoolean() &&
                root.TryGetProperty("companies", out var compsEl) && compsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in compsEl.EnumerateArray())
                {
                    var sym = el.TryGetProperty("symbol", out var sEl) ? sEl.GetString() : null;
                    if (string.IsNullOrWhiteSpace(sym)) continue;

                    var name = el.TryGetProperty("name", out var nEl) ? nEl.GetString() ?? sym : sym;
                    var sector = el.TryGetProperty("sector", out var secEl) ? secEl.GetString() ?? "Other" : "Other";
                    var category = el.TryGetProperty("category", out var catEl) ? catEl.GetString() : null;
                    decimal? mcap = null;
                    if (el.TryGetProperty("market_cap_mn", out var mEl) && mEl.TryGetDecimal(out var parsedMcap))
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
            RecordedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        _context.CompanyPriceHistories.Add(historyPoint);
        await _context.SaveChangesAsync(cancellationToken);

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
}

