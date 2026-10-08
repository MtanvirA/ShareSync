using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.DTOs.Inflation;

namespace ShareSync.Application.Services;

/// <summary>
/// FEATURE: Inflation-Adjusted Return (LF-28)
/// 
/// INFLATION: CPI Data
/// Provides access to official and calibrated historical consumer price index (CPI) annual inflation rates
/// sourced from the Bangladesh Bureau of Statistics (BBS) / Bangladesh Bank (for Bangladesh "BD")
/// and the U.S. Bureau of Labor Statistics (BLS) (for United States "US").
/// 
/// CALCULATION: Real Return (Fisher Equation)
/// Computes purchasing-power adjusted returns: Real Return = ((1 + Nominal Return) / (1 + Cumulative Inflation)) - 1.
/// 
/// BUSINESS RULE: Missing-Data Integrity
/// If any single year in the requested evaluation window is missing from both the relational database
/// and the verified baseline, the service returns null (unavailable) rather than silently assuming 0% inflation.
/// </summary>
public interface IInflationService
{
    /// <summary>
    /// INFLATION: Cumulative Rate Retrieval
    /// Calculates compounding multi-year inflation over a closed interval [startYear, endYear].
    /// </summary>
    Task<decimal?> GetCumulativeInflationRateAsync(string countryCode, int startYear, int endYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// CALCULATION: Real Return
    /// Formula: Real Return = [ (1 + Nominal / 100) / (1 + Inflation / 100) - 1 ] * 100
    /// </summary>
    decimal? CalculateRealReturn(decimal nominalReturnPct, decimal? inflationRatePct);

    /// <summary>
    /// FEATURE: Real Return for Period
    /// Combines cumulative inflation lookup and Fisher equation real return calculation.
    /// </summary>
    Task<RealReturnCalculationResultDto> CalculateRealReturnForPeriodAsync(string countryCode, decimal nominalReturnPct, int startYear, int endYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Backward-compatible overload defaulting to Bangladesh sovereign economic series ("BD").
    /// </summary>
    Task<RealReturnCalculationResultDto> CalculateRealReturnAsync(decimal nominalReturnPct, int startYear, int endYear, string countryCode = "BD", CancellationToken cancellationToken = default);

    /// <summary>
    /// INFLATION: Annual Series Retrieval
    /// Returns ordered chronological annual inflation rates for chart rendering and audits.
    /// </summary>
    Task<List<InflationRateDto>> GetHistoricalRatesAsync(string countryCode, CancellationToken cancellationToken = default);
}

/// <summary>
/// FEATURE: Inflation-Adjusted Return (LF-28)
/// 
/// PURPOSE:
/// Evaluates whether portfolio investment returns generated true economic purchasing power growth
/// after stripping out the erosive effects of annual consumer price index (CPI) currency inflation.
/// 
/// DATABASE: Table — inflation_rates
/// Interacts with the MySQL `inflation_rates` table via EF Core, utilizing a static fallback dictionary
/// for offline resilience and demonstration determinism.
/// </summary>
public class InflationService : IInflationService
{
    private readonly IApplicationDbContext _context;

    // INFLATION: Documented official baseline rates (Bangladesh Bank / BBS and US BLS)
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

    /// <summary>
    /// INFLATION: CPI Data
    /// CALCULATION: Cumulative Inflation
    /// 
    /// Formula:
    /// Cumulative Factor = Product_{y = startYear}^{endYear} (1 + Rate_y / 100)
    /// Cumulative Inflation (%) = (Cumulative Factor - 1.0) * 100
    /// 
    /// PURPOSE:
    /// Compounds individual annual inflation figures across the requested time window.
    /// 
    /// INPUT:
    /// countryCode: "BD" (Bangladesh) or "US" (United States).
    /// startYear, endYear: Integer years defining the historical measurement horizon.
    /// 
    /// OUTPUT:
    /// Compounded percentage inflation rate (e.g. 35.42%), or null if data is incomplete.
    /// 
    /// BUSINESS RULE: Missing-Data Integrity
    /// If any year in [startYear, endYear] is absent from DB and static baselines, returns null.
    /// Does not invent 0% to prevent misleading investors into false purchasing-power calculations.
    /// </summary>
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

    /// <summary>
    /// CALCULATION: Real Return (Fisher Equation)
    /// 
    /// Formula:
    /// (1 + r_real) = (1 + r_nominal) / (1 + i)
    /// Real Return (%) = [ ( (1 + Nominal / 100) / (1 + Inflation / 100) ) - 1 ] * 100
    /// 
    /// Meaning:
    /// Exact Fisher formula for purchasing power adjustment rather than crude linear subtraction (Nominal - Inflation).
    /// Linear subtraction overstates returns during high inflation periods (e.g., 9-10% inflation in BD).
    /// 
    /// Inputs:
    /// nominalReturnPct: Total percentage gain or loss of the portfolio/asset.
    /// inflationRatePct: Total cumulative CPI inflation rate over the matching timeframe.
    /// 
    /// Output:
    /// Real rate of return percentage rounded to 2 decimal places, or null if inflation data is missing.
    /// </summary>
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

    /// <summary>
    /// FEATURE: Inflation-Adjusted Return
    /// API: Real Return Calculation Endpoint Backend
    /// 
    /// Evaluates cumulative inflation and computes real return for a specific sovereign economy and date range.
    /// </summary>
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

    /// <summary>
    /// INFLATION: CPI Data Retrieval
    /// DATABASE: Table — inflation_rates
    /// 
    /// Fetches all registered annual CPI inflation records for the specified sovereign entity,
    /// sorted chronologically by year. Falls back to static documented benchmarks if the table is unpopulated.
    /// </summary>
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
