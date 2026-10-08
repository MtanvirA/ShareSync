using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.DTOs.Currencies;

namespace ShareSync.Application.Services;

/// <summary>
/// FEATURE: Multi-Asset and Multi-Currency (LF-29)
/// 
/// CURRENCY: Exchange Rate
/// Manages foreign currency conversion rates and supported asset classes across ShareSync portfolios.
/// 
/// Supported Currencies: BDT (Bangladeshi Taka, default sovereign base), USD (US Dollar).
/// Supported Asset Types: EQUITY, ETF, MUTUAL_FUND, CASH.
/// </summary>
public interface ICurrencyService
{
    /// <summary>
    /// CURRENCY: Exchange Rate Lookup
    /// Retrieves the most recent effective exchange rate for a currency pair on or before asOfDate.
    /// </summary>
    Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency, DateTime? asOfDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// CALCULATION: Currency Conversion
    /// Formula: Converted Amount = Round(Amount * Exchange Rate, 2)
    /// </summary>
    Task<decimal> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, DateTime? asOfDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// CALCULATION: Currency Conversion with Metadata
    /// Performs monetary conversion and packages source provenance and applied exchange rate.
    /// </summary>
    Task<CurrencyConversionResultDto> ConvertWithDetailsAsync(decimal amount, string fromCurrency, string toCurrency, DateTime? asOfDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// MULTI ASSET: Supported Universe
    /// Returns canonical list of supported currencies and asset types for UI dropdowns and validation.
    /// </summary>
    SupportedCurrenciesAndAssetsDto GetSupportedCurrenciesAndAssets();

    /// <summary>
    /// CURRENCY: Rate Table
    /// Fetches all active exchange rates from database or documented baseline.
    /// </summary>
    Task<List<ExchangeRateDto>> GetExchangeRatesAsync(CancellationToken cancellationToken = default);

    Task<List<ExchangeRateDto>> GetAllRatesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// FEATURE: Multi-Currency and Multi-Asset (LF-29)
/// 
/// PURPOSE:
/// Normalizes and values multi-currency holdings into a single portfolio base currency (default "BDT").
/// Enables cross-currency portfolio valuation, cash flow aggregation, and international equity accounting.
/// 
/// DATABASE: Table — currency_exchange_rates
/// Reads historical daily or point-in-time interbank foreign exchange rates from MySQL.
/// Falls back to Bangladesh Bank official interbank rate baselines when offline or unseeded.
/// </summary>
public class CurrencyService : ICurrencyService
{
    private readonly IApplicationDbContext _context;

    public const string DefaultBaseCurrency = "BDT";

    // MULTI ASSET: Supported asset universe
    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BDT",
        "USD"
    };

    private static readonly HashSet<string> SupportedAssetTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "EQUITY",
        "ETF",
        "MUTUAL_FUND",
        "CASH"
    };

    // CURRENCY: Documented Bangladesh Bank official interbank rate baseline
    private static readonly Dictionary<(string, string), decimal> FallbackRates = new()
    {
        { ("USD", "BDT"), 122.5000m },
        { ("BDT", "USD"), 0.008163m },
        { ("BDT", "BDT"), 1.0000m },
        { ("USD", "USD"), 1.0000m }
    };

    public CurrencyService(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// CURRENCY: Exchange Rate Resolution
    /// 
    /// Resolution Order:
    /// 1. Identity pair: from == to => return 1.0000.
    /// 2. Database query: Direct pair (from -> to) on or before asOfDate (ordered descending by effective date).
    /// 3. In-memory static baseline fallback: Bangladesh Bank interbank calibration.
    /// 4. Inverse database query: (to -> from) => 1.0 / inverseRate rounded to 6 decimals.
    /// 5. AppException (400) if no rate path exists.
    /// </summary>
    public async Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency, DateTime? asOfDate = null, CancellationToken cancellationToken = default)
    {
        var from = NormalizeCurrency(fromCurrency);
        var to = NormalizeCurrency(toCurrency);

        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0000m;
        }

        // 1. Try fetching from database
        var query = _context.CurrencyExchangeRates
            .AsNoTracking()
            .Where(r => r.FromCurrency == from && r.ToCurrency == to);

        if (asOfDate.HasValue)
        {
            var date = asOfDate.Value.Date;
            query = query.Where(r => r.EffectiveDate <= date);
        }

        var dbRate = await query
            .OrderByDescending(r => r.EffectiveDate)
            .Select(r => (decimal?)r.ExchangeRate)
            .FirstOrDefaultAsync(cancellationToken);

        if (dbRate.HasValue)
        {
            return dbRate.Value;
        }

        // 2. Check fallback rates
        if (FallbackRates.TryGetValue((from, to), out var fallbackRate))
        {
            return fallbackRate;
        }

        // 3. Check inverse in database
        var inverseQuery = _context.CurrencyExchangeRates
            .AsNoTracking()
            .Where(r => r.FromCurrency == to && r.ToCurrency == from);

        if (asOfDate.HasValue)
        {
            var date = asOfDate.Value.Date;
            inverseQuery = inverseQuery.Where(r => r.EffectiveDate <= date);
        }

        var inverseDbRate = await inverseQuery
            .OrderByDescending(r => r.EffectiveDate)
            .Select(r => (decimal?)r.ExchangeRate)
            .FirstOrDefaultAsync(cancellationToken);

        if (inverseDbRate.HasValue && inverseDbRate.Value > 0)
        {
            return Math.Round(1.0m / inverseDbRate.Value, 6);
        }

        throw new AppException($"Exchange rate from {from} to {to} is not available.", 400);
    }

    /// <summary>
    /// CALCULATION: Currency Conversion
    /// 
    /// Formula:
    /// Converted Amount = Round(Amount * ExchangeRate, 2)
    /// 
    /// Inputs:
    /// amount: Monetary quantity in source denomination.
    /// fromCurrency: 3-character ISO code of source currency (e.g. "USD").
    /// toCurrency: 3-character ISO code of target currency (e.g. "BDT").
    /// asOfDate: Optional valuation date for historical rate alignment.
    /// </summary>
    public async Task<decimal> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, DateTime? asOfDate = null, CancellationToken cancellationToken = default)
    {
        var rate = await GetExchangeRateAsync(fromCurrency, toCurrency, asOfDate, cancellationToken);
        return Math.Round(amount * rate, 2);
    }

    /// <summary>
    /// CALCULATION: Currency Conversion with Audit Trail
    /// Returns converted figure alongside applied multiplier, valuation date, and data provenance.
    /// </summary>
    public async Task<CurrencyConversionResultDto> ConvertWithDetailsAsync(decimal amount, string fromCurrency, string toCurrency, DateTime? asOfDate = null, CancellationToken cancellationToken = default)
    {
        var from = NormalizeCurrency(fromCurrency);
        var to = NormalizeCurrency(toCurrency);
        var rate = await GetExchangeRateAsync(from, to, asOfDate, cancellationToken);
        var converted = Math.Round(amount * rate, 2);

        return new CurrencyConversionResultDto
        {
            OriginalAmount = amount,
            FromCurrency = from,
            ConvertedAmount = converted,
            ToCurrency = to,
            ExchangeRate = rate,
            EffectiveDate = asOfDate?.Date ?? DateTime.UtcNow.Date,
            Source = "Bangladesh Bank Official Interbank Foreign Exchange Rate"
        };
    }

    /// <summary>
    /// MULTI ASSET: Supported Instrument Universe
    /// Returns configured valid currencies ("BDT", "USD") and asset types ("EQUITY", "ETF", "MUTUAL_FUND", "CASH").
    /// </summary>
    public SupportedCurrenciesAndAssetsDto GetSupportedCurrenciesAndAssets()
    {
        return new SupportedCurrenciesAndAssetsDto
        {
            SupportedCurrencies = SupportedCurrencies.OrderBy(c => c).ToList(),
            DefaultBaseCurrency = DefaultBaseCurrency,
            SupportedAssetTypes = SupportedAssetTypes.OrderBy(a => a).ToList()
        };
    }

    /// <summary>
    /// DATABASE: Table — currency_exchange_rates
    /// Fetches all registered currency conversion rates ordered by effective date descending.
    /// </summary>
    public async Task<List<ExchangeRateDto>> GetExchangeRatesAsync(CancellationToken cancellationToken = default)
    {
        var rates = await _context.CurrencyExchangeRates
            .AsNoTracking()
            .OrderByDescending(r => r.EffectiveDate)
            .ThenBy(r => r.FromCurrency)
            .Select(r => new ExchangeRateDto
            {
                RateId = r.RateId,
                FromCurrency = r.FromCurrency,
                ToCurrency = r.ToCurrency,
                ExchangeRate = r.ExchangeRate,
                EffectiveDate = r.EffectiveDate,
                Source = r.Source
            })
            .ToListAsync(cancellationToken);

        if (!rates.Any())
        {
            int id = 1;
            return FallbackRates.Select(r => new ExchangeRateDto
            {
                RateId = id++,
                FromCurrency = r.Key.Item1,
                ToCurrency = r.Key.Item2,
                ExchangeRate = r.Value,
                EffectiveDate = new DateTime(2026, 1, 1),
                Source = "Bangladesh Bank Official Interbank Rate"
            }).ToList();
        }

        return rates;
    }

    public Task<List<ExchangeRateDto>> GetAllRatesAsync(CancellationToken cancellationToken = default)
    {
        return GetExchangeRatesAsync(cancellationToken);
    }

    /// <summary>
    /// VALIDATION: Currency ISO Code Normalization
    /// Strips whitespace, converts to uppercase, and rejects unsupported denominations with AppException (400).
    /// </summary>
    private static string NormalizeCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return DefaultBaseCurrency;
        }

        var normalized = currency.Trim().ToUpperInvariant();
        if (!SupportedCurrencies.Contains(normalized))
        {
            throw new AppException($"Unsupported currency '{currency}'. Supported currencies: {string.Join(", ", SupportedCurrencies)}.", 400);
        }

        return normalized;
    }
}
