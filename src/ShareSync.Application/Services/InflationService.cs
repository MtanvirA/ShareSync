using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.DTOs.Inflation;

namespace ShareSync.Application.Services;

public interface IInflationService
{
    Task<decimal?> GetCumulativeInflationRateAsync(string countryCode, int startYear, int endYear, CancellationToken cancellationToken = default);
    decimal? CalculateRealReturn(decimal nominalReturnPct, decimal? inflationRatePct);
    Task<RealReturnCalculationResultDto> CalculateRealReturnForPeriodAsync(string countryCode, decimal nominalReturnPct, int startYear, int endYear, CancellationToken cancellationToken = default);
    Task<RealReturnCalculationResultDto> CalculateRealReturnAsync(decimal nominalReturnPct, int startYear, int endYear, string countryCode = "BD", CancellationToken cancellationToken = default);
    Task<List<InflationRateDto>> GetHistoricalRatesAsync(string countryCode, CancellationToken cancellationToken = default);
}

public class InflationService : IInflationService
{
    private readonly IApplicationDbContext _context;

    // Documented official baseline rates (Bangladesh Bank / Bangladesh Bureau of Statistics and US BLS)
    private static readonly Dictionary<(string, int), decimal> StaticBaselineRates = new()
    {
        { ("BD", 2020), 5.65m },
        { ("BD", 2021), 5.56m },
        { ("BD", 2022), 6.15m },
        { ("BD", 2023), 9.02m },
        { ("BD", 2024), 9.73m },
        { ("BD", 2025), 10.20m },
        { ("US", 2020), 1.23m },
        { ("US", 2021), 4.70m },
        { ("US", 2022), 8.00m },
        { ("US", 2023), 4.12m },
        { ("US", 2024), 2.97m },
        { ("US", 2025), 2.80m }
    };

    public InflationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<decimal?> GetCumulativeInflationRateAsync(string countryCode, int startYear, int endYear, CancellationToken cancellationToken = default)
    {
        if (startYear > endYear)
        {
            (startYear, endYear) = (endYear, startYear);
        }

        var normalizedCountry = string.IsNullOrWhiteSpace(countryCode) ? "BD" : countryCode.Trim().ToUpperInvariant();

        // 1. Query database for rates in [startYear, endYear]
        var dbRates = await _context.InflationRates
            .AsNoTracking()
            .Where(r => r.CountryCode == normalizedCountry && r.Year >= startYear && r.Year <= endYear)
            .ToDictionaryAsync(r => r.Year, r => r.Rate, cancellationToken);

        // 2. Verify all years in the range exist
        decimal cumulativeFactor = 1.0m;
        for (int y = startYear; y <= endYear; y++)
        {
            if (dbRates.TryGetValue(y, out var rate))
            {
                cumulativeFactor *= (1.0m + (rate / 100.0m));
            }
            else if (StaticBaselineRates.TryGetValue((normalizedCountry, y), out var staticRate))
            {
                cumulativeFactor *= (1.0m + (staticRate / 100.0m));
            }
            else
            {
                // Missing inflation period: do NOT silently assume zero, do NOT invent values
                return null;
            }
        }

        // Return cumulative inflation rate percentage
        return Math.Round((cumulativeFactor - 1.0m) * 100.0m, 2);
    }

    public decimal? CalculateRealReturn(decimal nominalReturnPct, decimal? inflationRatePct)
    {
        if (!inflationRatePct.HasValue)
        {
            return null;
        }

        var inflationFactor = 1.0m + (inflationRatePct.Value / 100.0m);
        if (inflationFactor == 0m)
        {
            return 0m;
        }

        var nominalFactor = 1.0m + (nominalReturnPct / 100.0m);
        var realFactor = (nominalFactor / inflationFactor) - 1.0m;
        return Math.Round(realFactor * 100.0m, 2);
    }

    public async Task<RealReturnCalculationResultDto> CalculateRealReturnForPeriodAsync(
        string countryCode,
        decimal nominalReturnPct,
        int startYear,
        int endYear,
        CancellationToken cancellationToken = default)
    {
        var normalizedCountry = string.IsNullOrWhiteSpace(countryCode) ? "BD" : countryCode.Trim().ToUpperInvariant();
        var inflationRate = await GetCumulativeInflationRateAsync(normalizedCountry, startYear, endYear, cancellationToken);

        var result = new RealReturnCalculationResultDto
        {
            NominalReturnPercentage = Math.Round(nominalReturnPct, 2),
            InflationRatePercentage = inflationRate,
            CountryCode = normalizedCountry,
            Period = startYear == endYear ? $"{startYear}" : $"{startYear} - {endYear}"
        };

        if (inflationRate.HasValue)
        {
            result.RealReturnPercentage = CalculateRealReturn(nominalReturnPct, inflationRate.Value);
            result.IsDataAvailable = true;
            result.Status = "Adjusted via official CPI inflation series";
        }
        else
        {
            result.RealReturnPercentage = null;
            result.IsDataAvailable = false;
            result.Status = $"Inflation data unavailable for period {result.Period} ({normalizedCountry})";
        }

        return result;
    }

    public Task<RealReturnCalculationResultDto> CalculateRealReturnAsync(
        decimal nominalReturnPct,
        int startYear,
        int endYear,
        string countryCode = "BD",
        CancellationToken cancellationToken = default)
    {
        return CalculateRealReturnForPeriodAsync(countryCode, nominalReturnPct, startYear, endYear, cancellationToken);
    }

    public async Task<List<InflationRateDto>> GetHistoricalRatesAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        var normalizedCountry = string.IsNullOrWhiteSpace(countryCode) ? "BD" : countryCode.Trim().ToUpperInvariant();

        var dbRates = await _context.InflationRates
            .AsNoTracking()
            .Where(r => r.CountryCode == normalizedCountry)
            .OrderBy(r => r.Year)
            .Select(r => new InflationRateDto
            {
                RateId = r.RateId,
                CountryCode = r.CountryCode,
                Year = r.Year,
                InflationRate = r.Rate,
                Source = r.Source
            })
            .ToListAsync(cancellationToken);

        if (!dbRates.Any())
        {
            // Fallback to documented academic/demo baseline
            return StaticBaselineRates
                .Where(kvp => kvp.Key.Item1 == normalizedCountry)
                .OrderBy(kvp => kvp.Key.Item2)
                .Select((kvp, idx) => new InflationRateDto
                {
                    RateId = idx + 1,
                    CountryCode = kvp.Key.Item1,
                    Year = kvp.Key.Item2,
                    InflationRate = kvp.Value,
                    Source = normalizedCountry == "BD" ? "Bangladesh Bureau of Statistics (BBS) / Bangladesh Bank" : "U.S. Bureau of Labor Statistics (BLS)"
                })
                .ToList();
        }

        return dbRates;
    }
}
