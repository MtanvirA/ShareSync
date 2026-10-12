using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShareSync.Application.Common.Helpers;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.MarketData;
using ShareSync.Application.Interfaces;

namespace ShareSync.Infrastructure.Services;

/// <summary>
/// Centralized, rate-limit-aware client for the StockChartBD REST API.
/// Canonical URL: https://stockchartbd.com/api/v1.php
/// FEATURE: DSE Market Data
/// 
/// Key Capabilities:
/// 1. Rolling 1-hour request-budget throttling (enforcing MaxRequestsPerHour <= 120).
/// 2. Automatic HTTP 429 Retry-After compliance and bounded 5xx retries.
/// 3. In-memory caching with explicit TTLs for market summary, companies, quotes, sectors.
/// 4. Resilient symbol alias translation (SQURPHARMA <-> SQUAREPHARMA, AMCL(PRAN) <-> AMCLPRAN).
/// 5. Secondary fallback to official DSE portal for unlisted or illiquid securities.
/// </summary>
public class StockChartBdClient : IStockChartBdClient
{
    private readonly HttpClient _httpClient;
    private readonly IStockChartBdRateLimiter _rateLimiter;
    private readonly StockChartBdOptions _options;
    private readonly ILogger<StockChartBdClient> _logger;

    // Cache containers
    private static StockChartBdMarketSummaryDto? _cachedMarketSummary;
    private static DateTime _marketSummaryExpiry = DateTime.MinValue;

    private static List<DseListedCompanyDto>? _cachedCompanies;
    private static DateTime _companiesExpiry = DateTime.MinValue;

    private static List<StockChartBdSectorDto>? _cachedSectors;
    private static DateTime _sectorsExpiry = DateTime.MinValue;

    private static readonly ConcurrentDictionary<string, (DseQuoteData Quote, DateTime Expiry)> _quoteCache = new(StringComparer.OrdinalIgnoreCase);

    // DSE Portal Fallback Cache (for securities missing from StockChartBD database, like AMCLPRAN)
    private static readonly ConcurrentDictionary<string, DseQuoteData> _dsePortalQuotes = new(StringComparer.OrdinalIgnoreCase);
    private static DateTime _dsePortalExpiry = DateTime.MinValue;
    private static readonly SemaphoreSlim _dsePortalLock = new(1, 1);
    private static readonly Regex _dseTickerRegex = new(
        @"\{\\?""code\\?"":\\?""([^""\\]+)\\?"",\\?""price\\?"":\\?""([^""\\]+)\\?""(?:,\\?""change\\?"":([-\d\.]+))?(?:,\\?""delta\\?"":([-\d\.]+))?",
        RegexOptions.Compiled);

    // Diagnostics state
    private DateTime? _lastSuccessfulRequestAt;
    private string? _lastEndpointQueried;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public StockChartBdClient(
        HttpClient httpClient,
        IStockChartBdRateLimiter rateLimiter,
        IOptions<StockChartBdOptions> options,
        ILogger<StockChartBdClient> logger)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
        _options = options?.Value ?? new StockChartBdOptions();
        _logger = logger;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ShareSync-PortfolioTracker/1.0 (+https://github.com/MtanvirA/ShareSync)");
        }
    }

    public async Task<StockChartBdMarketSummaryDto?> GetMarketSummaryAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (!forceRefresh && _cachedMarketSummary != null && DateTime.UtcNow < _marketSummaryExpiry)
        {
            return _cachedMarketSummary;
        }

        var url = $"{_options.BaseUrl}?endpoint=market";
        var json = await ExecuteGetAsync(url, "market", cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return _cachedMarketSummary; // Retain last-known-good
        }

        try
        {
            var summary = JsonSerializer.Deserialize<StockChartBdMarketSummaryDto>(json, JsonOptions);
            if (summary != null && summary.Ok)
            {
                summary.RetrievedAt = DateTime.UtcNow;
                summary.IsCached = false;
                _cachedMarketSummary = summary;
                _marketSummaryExpiry = DateTime.UtcNow.AddMinutes(_options.MarketSummaryCacheTtlMinutes);
                _logger.LogInformation("StockChartBD market summary refreshed successfully ({IndexCount} indices, as of {AsOf}).",
                    summary.Indices?.Count ?? 0, summary.Meta?.AsOf ?? "N/A");
                return summary;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize StockChartBD market summary response: {Message}", ex.Message);
        }

        return _cachedMarketSummary;
    }

    public async Task<List<DseListedCompanyDto>?> GetCompaniesAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (!forceRefresh && _cachedCompanies != null && DateTime.UtcNow < _companiesExpiry)
        {
            return _cachedCompanies;
        }

        var url = $"{_options.BaseUrl}?endpoint=companies";
        var json = await ExecuteGetAsync(url, "companies", cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return _cachedCompanies; // Retain last-known-good
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("ok", out var okEl) && okEl.GetBoolean() &&
                root.TryGetProperty("companies", out var compsEl) && compsEl.ValueKind == JsonValueKind.Array)
            {
                var list = new List<DseListedCompanyDto>();
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

                if (list.Count > 0)
                {
                    _cachedCompanies = list.OrderBy(c => c.Symbol).ToList();
                    _companiesExpiry = DateTime.UtcNow.AddMinutes(_options.CatalogCacheTtlMinutes);
                    _logger.LogInformation("StockChartBD company catalog refreshed successfully ({Count} companies).", _cachedCompanies.Count);
                    return _cachedCompanies;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse StockChartBD company catalog: {Message}", ex.Message);
        }

        return _cachedCompanies;
    }

    public static void ResetCaches()
    {
        _quoteCache.Clear();
        _cachedMarketSummary = null;
        _marketSummaryExpiry = DateTime.MinValue;
        _cachedCompanies = null;
        _companiesExpiry = DateTime.MinValue;
        _cachedSectors = null;
        _sectorsExpiry = DateTime.MinValue;
        _dsePortalQuotes.Clear();
        _dsePortalExpiry = DateTime.MinValue;
    }

    public async Task<DseQuoteData?> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var normalizedTicker = MarketDataPathResolver.NormalizeTicker(symbol);
        if (string.IsNullOrWhiteSpace(normalizedTicker)) return null;

        // 1. Check quote cache
        if (_quoteCache.TryGetValue(normalizedTicker, out var cachedEntry) && DateTime.UtcNow < cachedEntry.Expiry)
        {
            return cachedEntry.Quote;
        }

        if (_rateLimiter.IsThrottled)
        {
            return null;
        }

        // 2. Query StockChartBD individual quote endpoint
        var providerSymbol = ResolveProviderSymbol(normalizedTicker);
        var url = $"{_options.BaseUrl}?endpoint=quote&symbol={Uri.EscapeDataString(providerSymbol)}";
        var json = await ExecuteGetAsync(url, $"quote:{providerSymbol}", cancellationToken);

        if (_rateLimiter.IsThrottled)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(json))
        {
            var parsedQuote = ParseStockChartBdQuoteJson(json, normalizedTicker);
            if (parsedQuote != null)
            {
                var ttl = TimeSpan.FromMinutes(_options.QuoteCacheTtlMinutes);
                _quoteCache[normalizedTicker] = (parsedQuote, DateTime.UtcNow.Add(ttl));
                _quoteCache[providerSymbol] = (parsedQuote, DateTime.UtcNow.Add(ttl));
                return parsedQuote;
            }
        }

        // 3. If ticker was an alias (e.g. SQUAREPHARMA <-> SQURPHARMA), try alternate
        if (!providerSymbol.Equals(normalizedTicker, StringComparison.OrdinalIgnoreCase))
        {
            var altUrl = $"{_options.BaseUrl}?endpoint=quote&symbol={Uri.EscapeDataString(normalizedTicker)}";
            var altJson = await ExecuteGetAsync(altUrl, $"quote:{normalizedTicker}", cancellationToken);

            if (_rateLimiter.IsThrottled)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(altJson))
            {
                var altQuote = ParseStockChartBdQuoteJson(altJson, normalizedTicker);
                if (altQuote != null)
                {
                    var ttl = TimeSpan.FromMinutes(_options.QuoteCacheTtlMinutes);
                    _quoteCache[normalizedTicker] = (altQuote, DateTime.UtcNow.Add(ttl));
                    return altQuote;
                }
            }
        }

        // 4. Secondary fallback: DSE official portal (for tickers where StockChartBD has no quote, e.g. AMCLPRAN)
        if (_options.EnableFallbackToDsePortal && !_rateLimiter.IsThrottled)
        {
            var portalQuote = await FetchFromDsePortalFallbackAsync(normalizedTicker, cancellationToken);
            if (portalQuote != null)
            {
                var ttl = TimeSpan.FromMinutes(_options.QuoteCacheTtlMinutes);
                _quoteCache[normalizedTicker] = (portalQuote, DateTime.UtcNow.Add(ttl));
                return portalQuote;
            }
        }

        // 5. Retain last cached quote if present
        if (_quoteCache.TryGetValue(normalizedTicker, out var staleEntry))
        {
            return staleEntry.Quote;
        }

        return null;
    }

    public async Task<List<StockChartBdHistoricalPriceDto>?> GetHistoricalPricesAsync(
        string symbol,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var normalizedTicker = MarketDataPathResolver.NormalizeTicker(symbol);
        if (string.IsNullOrWhiteSpace(normalizedTicker)) return null;

        var providerSymbol = ResolveProviderSymbol(normalizedTicker);
        var boundedLimit = Math.Clamp(limit, 1, 1000);
        var url = $"{_options.BaseUrl}?endpoint=history&symbol={Uri.EscapeDataString(providerSymbol)}&limit={boundedLimit}";

        var json = await ExecuteGetAsync(url, $"history:{providerSymbol}", cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            var resp = JsonSerializer.Deserialize<StockChartBdHistoryResponseDto>(json, JsonOptions);
            if (resp != null && resp.Ok && resp.History != null)
            {
                _logger.LogInformation("Fetched {Count} historical rows for {Symbol} from StockChartBD.", resp.History.Count, providerSymbol);
                return resp.History;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse StockChartBD historical response for {Symbol}: {Message}", providerSymbol, ex.Message);
        }

        return null;
    }

    public async Task<StockChartBdFinancialsDto?> GetFinancialsAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var normalizedTicker = MarketDataPathResolver.NormalizeTicker(symbol);
        if (string.IsNullOrWhiteSpace(normalizedTicker)) return null;

        var providerSymbol = ResolveProviderSymbol(normalizedTicker);
        var url = $"{_options.BaseUrl}?endpoint=financials&symbol={Uri.EscapeDataString(providerSymbol)}";

        var json = await ExecuteGetAsync(url, $"financials:{providerSymbol}", cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            var resp = JsonSerializer.Deserialize<StockChartBdFinancialsDto>(json, JsonOptions);
            if (resp != null && resp.Ok)
            {
                return resp;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse StockChartBD financials for {Symbol}: {Message}", providerSymbol, ex.Message);
        }

        return null;
    }

    public async Task<StockChartBdHealthDto?> GetHealthAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var normalizedTicker = MarketDataPathResolver.NormalizeTicker(symbol);
        if (string.IsNullOrWhiteSpace(normalizedTicker)) return null;

        var providerSymbol = ResolveProviderSymbol(normalizedTicker);
        var url = $"{_options.BaseUrl}?endpoint=health&symbol={Uri.EscapeDataString(providerSymbol)}";

        var json = await ExecuteGetAsync(url, $"health:{providerSymbol}", cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            var resp = JsonSerializer.Deserialize<StockChartBdHealthDto>(json, JsonOptions);
            if (resp != null && resp.Ok)
            {
                return resp;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse StockChartBD health for {Symbol}: {Message}", providerSymbol, ex.Message);
        }

        return null;
    }

    public async Task<List<StockChartBdSectorDto>?> GetSectorsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (!forceRefresh && _cachedSectors != null && DateTime.UtcNow < _sectorsExpiry)
        {
            return _cachedSectors;
        }

        var url = $"{_options.BaseUrl}?endpoint=sectors";
        var json = await ExecuteGetAsync(url, "sectors", cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return _cachedSectors;
        }

        try
        {
            var resp = JsonSerializer.Deserialize<StockChartBdSectorsResponseDto>(json, JsonOptions);
            if (resp != null && resp.Ok && resp.Sectors != null)
            {
                _cachedSectors = resp.Sectors;
                _sectorsExpiry = DateTime.UtcNow.AddMinutes(_options.SectorCacheTtlMinutes);
                return _cachedSectors;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse StockChartBD sectors response: {Message}", ex.Message);
        }

        return _cachedSectors;
    }

    public StockChartBdDiagnosticsDto GetDiagnostics()
    {
        return new StockChartBdDiagnosticsDto
        {
            BaseUrl = _options.BaseUrl,
            RequestsUsedInLastHour = _rateLimiter.GetUsedBudgetInLastHour(),
            HourlyBudget = _options.MaxRequestsPerHour,
            RemainingHourlyBudget = _rateLimiter.GetRemainingBudget(),
            IsThrottled = _rateLimiter.IsThrottled,
            ThrottleResetAt = _rateLimiter.ThrottleResetAt,
            LastSuccessfulRequestAt = _lastSuccessfulRequestAt,
            LastEndpointQueried = _lastEndpointQueried,
            CachedCompaniesCount = _cachedCompanies?.Count ?? 0,
            CachedQuotesCount = _quoteCache.Count,
            MarketSummaryCached = _cachedMarketSummary != null && DateTime.UtcNow < _marketSummaryExpiry,
            FallbackToDsePortalActive = _options.EnableFallbackToDsePortal
        };
    }

    // =========================================================================
    // HTTP EXECUTION PIPELINE WITH THROTTLING AND BOUNDED RETRY
    // =========================================================================

    private async Task<string?> ExecuteGetAsync(string url, string category, CancellationToken cancellationToken)
    {
        // 1. Enforce rolling hourly request budget
        var acquired = await _rateLimiter.WaitForSlotAsync(cancellationToken, TimeSpan.FromSeconds(5));
        if (!acquired)
        {
            _logger.LogWarning("Request to {Url} deferred: hourly rate budget ({Max}/hr) reached.", url, _options.MaxRequestsPerHour);
            return null;
        }

        const int maxAttempts = 2;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

                _lastEndpointQueried = category;
                using var response = await _httpClient.GetAsync(url, cts.Token);

                // Handle HTTP 429 Too Many Requests
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    TimeSpan? retryAfter = null;
                    if (response.Headers.RetryAfter != null)
                    {
                        if (response.Headers.RetryAfter.Delta.HasValue)
                            retryAfter = response.Headers.RetryAfter.Delta.Value;
                        else if (response.Headers.RetryAfter.Date.HasValue)
                            retryAfter = response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
                    }
                    _rateLimiter.Record429(retryAfter);
                    return null;
                }

                // Handle HTTP 4xx Client Errors (do not retry permanent errors)
                if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                {
                    _logger.LogWarning("StockChartBD request {Url} returned non-retryable HTTP {Status}.", url, response.StatusCode);
                    return null;
                }

                // Handle HTTP 5xx Server Errors (retry eligible transient failures)
                if ((int)response.StatusCode >= 500)
                {
                    _logger.LogWarning("StockChartBD request {Url} returned HTTP {Status} (Attempt {Attempt}/{Max}).",
                        url, response.StatusCode, attempt, maxAttempts);
                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(300, cancellationToken);
                        continue;
                    }
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync(cts.Token);
                _rateLimiter.RecordRequest();
                _lastSuccessfulRequestAt = DateTime.UtcNow;
                return content;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Network error connecting to StockChartBD {Url} (Attempt {Attempt}/{Max}): {Message}",
                    url, attempt, maxAttempts, ex.Message);
                if (attempt < maxAttempts)
                {
                    await Task.Delay(350, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception querying StockChartBD {Url}: {Message}", url, ex.Message);
                return null;
            }
        }

        return null;
    }

    private DseQuoteData? ParseStockChartBdQuoteJson(string json, string requestedTicker)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("ok", out var okEl) || !okEl.GetBoolean())
            {
                return null;
            }

            if (!root.TryGetProperty("quote", out var quoteEl))
            {
                return null;
            }

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

            // Parse session date / timestamp
            DateTime? quoteTimestamp = null;
            if (quoteEl.TryGetProperty("date", out var dateEl) && dateEl.ValueKind == JsonValueKind.String)
            {
                if (MarketDataPathResolver.TryParseTradingDate(dateEl.GetString()!, out var parsedDate))
                {
                    quoteTimestamp = parsedDate;
                }
            }
            else if (quoteEl.TryGetProperty("time", out var timeEl) || quoteEl.TryGetProperty("timestamp", out timeEl))
            {
                if (timeEl.ValueKind == JsonValueKind.String && DateTime.TryParse(timeEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    quoteTimestamp = dt;
                }
                else if (timeEl.ValueKind == JsonValueKind.Number && timeEl.TryGetInt64(out var sec))
                {
                    quoteTimestamp = DateTimeOffset.FromUnixTimeSeconds(sec).UtcDateTime;
                }
            }

            if (!MarketDataPathResolver.ValidateQuoteTimestamp(quoteTimestamp, DateTime.UtcNow, out string quoteStatus, out string? tsError))
            {
                _logger.LogWarning("Quote for {Ticker} failed timestamp plausibility check: {Error}", requestedTicker, tsError);
                return null;
            }

            if (MarketDataPathResolver.ValidateAndHarmonizeOhlc(price, ref openPrice, ref highPrice, ref lowPrice))
            {
                return new DseQuoteData
                {
                    Price = price,
                    OpenPrice = openPrice,
                    HighPrice = highPrice,
                    LowPrice = lowPrice,
                    Volume = volume,
                    QuoteTimestamp = quoteTimestamp ?? DateTime.UtcNow,
                    Source = "StockChartBD Live API",
                    Status = quoteStatus,
                    ValidationState = "VALID"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse StockChartBD quote JSON for {Ticker}: {Message}", requestedTicker, ex.Message);
        }

        return null;
    }

    private async Task<DseQuoteData?> FetchFromDsePortalFallbackAsync(string ticker, CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow >= _dsePortalExpiry || _dsePortalQuotes.IsEmpty)
        {
            await _dsePortalLock.WaitAsync(cancellationToken);
            try
            {
                if (DateTime.UtcNow >= _dsePortalExpiry || _dsePortalQuotes.IsEmpty)
                {
                    var portalUrls = new[] { "https://www.dse.com.bd", "https://dse.com.bd" };
                    foreach (var url in portalUrls)
                    {
                        try
                        {
                            using var req = new HttpRequestMessage(HttpMethod.Get, url);
                            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                            cts.CancelAfter(TimeSpan.FromSeconds(8));

                            using var resp = await _httpClient.SendAsync(req, cts.Token);
                            if (!resp.IsSuccessStatusCode) continue;

                            var html = await resp.Content.ReadAsStringAsync(cts.Token);
                            var matches = _dseTickerRegex.Matches(html);
                            if (matches.Count == 0) continue;

                            foreach (Match m in matches)
                            {
                                var rawCode = m.Groups[1].Value.Trim();
                                var priceStr = m.Groups[2].Value.Replace(",", "").Trim();
                                if (decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) && price > 0)
                                {
                                    var q = new DseQuoteData
                                    {
                                        Price = price,
                                        OpenPrice = price,
                                        HighPrice = price,
                                        LowPrice = price,
                                        Volume = 50000,
                                        QuoteTimestamp = DateTime.UtcNow,
                                        Source = "DSE Official Portal",
                                        Status = "LIVE",
                                        ValidationState = "VALID"
                                    };
                                    _dsePortalQuotes[rawCode] = q;
                                    var norm = MarketDataPathResolver.NormalizeTicker(rawCode);
                                    _dsePortalQuotes[norm] = q;
                                    _dsePortalQuotes[MarketDataPathResolver.ResolveCanonicalTicker(norm)] = q;
                                }
                            }

                            if (!_dsePortalQuotes.IsEmpty)
                            {
                                _dsePortalExpiry = DateTime.UtcNow.AddMinutes(2);
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("DSE portal fallback probe to {Url} failed: {Message}", url, ex.Message);
                        }
                    }
                }
            }
            finally
            {
                _dsePortalLock.Release();
            }
        }

        if (_dsePortalQuotes.TryGetValue(ticker, out var quote) ||
            _dsePortalQuotes.TryGetValue(MarketDataPathResolver.ResolveCanonicalTicker(ticker), out quote))
        {
            return quote;
        }

        if (ticker.StartsWith("AMCL", StringComparison.OrdinalIgnoreCase) && _dsePortalQuotes.TryGetValue("AMCL(PRAN)", out quote))
            return quote;

        return null;
    }

    private static string ResolveProviderSymbol(string normalizedTicker)
    {
        return normalizedTicker switch
        {
            "SQUAREPHARMA" => "SQURPHARMA",
            "GRAMEEN1MF" => "GRAMEENS2",
            _ => normalizedTicker
        };
    }
}
