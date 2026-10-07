using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.DTOs.Currencies;

namespace ShareSync.Application.Services;

public interface ICurrencyService
{
    Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency, DateTime? asOfDate = null, CancellationToken cancellationToken = default);
    Task<decimal> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, DateTime? asOfDate = null, CancellationToken cancellationToken = default);
    Task<CurrencyConversionResultDto> ConvertWithDetailsAsync(decimal amount, string fromCurrency, string toCurrency, DateTime? asOfDate = null, CancellationToken cancellationToken = default);
    SupportedCurrenciesAndAssetsDto GetSupportedCurrenciesAndAssets();
    Task<List<ExchangeRateDto>> GetExchangeRatesAsync(CancellationToken cancellationToken = default);
    Task<List<ExchangeRateDto>> GetAllRatesAsync(CancellationToken cancellationToken = default);
}

public class CurrencyService : ICurrencyService
{
    private readonly IApplicationDbContext _context;

    public const string DefaultBaseCurrency = "BDT";

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

    // Documented Bangladesh Bank official interbank rate baseline
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

        // Check inverse in database
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

    public async Task<decimal> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, DateTime? asOfDate = null, CancellationToken cancellationToken = default)
    {
        var rate = await GetExchangeRateAsync(fromCurrency, toCurrency, asOfDate, cancellationToken);
        return Math.Round(amount * rate, 2);
    }

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

    public SupportedCurrenciesAndAssetsDto GetSupportedCurrenciesAndAssets()
    {
        return new SupportedCurrenciesAndAssetsDto
        {
            SupportedCurrencies = SupportedCurrencies.OrderBy(c => c).ToList(),
            DefaultBaseCurrency = DefaultBaseCurrency,
            SupportedAssetTypes = SupportedAssetTypes.OrderBy(a => a).ToList()
        };
    }

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
