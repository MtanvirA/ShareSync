using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.DTOs.Inflation;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Services;

/// <summary>
/// FEATURE: Inflation and Real Return
/// 
/// INFLATION: CPI Data
/// Provides access to official and calibrated historical consumer price index (CPI) annual inflation rates
/// and monthly CPI index levels sourced from the Bangladesh Bureau of Statistics (BBS) / Bangladesh Bank (for Bangladesh "BD")
/// and the U.S. Bureau of Labor Statistics (BLS) (for United States "US").
/// 
/// CALCULATION: Real Return (Fisher Equation)
/// Computes purchasing-power adjusted returns: Real Return = ((1 + Nominal Return) / (1 + Cumulative Inflation)) - 1.
/// 
/// BUSINESS RULE: Missing-Data Integrity
/// If any single period in the requested evaluation window is missing from both the relational database
/// and the verified baseline, the service returns null (unavailable) rather than silently assuming 0% inflation.
/// </summary>
public interface IInflationService
{
    /// <summary>
    /// INFLATION: Cumulative Rate Retrieval (Annual compounding)
    /// Calculates compounding multi-year inflation over a closed interval [startYear, endYear].
    /// </summary>
    Task<decimal?> GetCumulativeInflationRateAsync(string countryCode, int startYear, int endYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// INFLATION: Date-Based Cumulative Rate Retrieval (Monthly CPI Index Level)
    /// Formula: Cumulative Inflation (%) = ((Ending CPI Index / Starting CPI Index) - 1) * 100
    /// </summary>
    Task<decimal?> GetCumulativeInflationRateForPeriodAsync(string countryCode, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// CALCULATION: Real Return (Fisher Equation)
    /// Formula: Real Return = [ (1 + Nominal / 100) / (1 + Inflation / 100) - 1 ] * 100
    /// </summary>
    decimal? CalculateRealReturn(decimal nominalReturnPct, decimal? inflationRatePct);

    /// <summary>
    /// FEATURE: Real Return for Annual Period
    /// Combines cumulative inflation lookup and Fisher equation real return calculation.
    /// </summary>
    Task<RealReturnCalculationResultDto> CalculateRealReturnForPeriodAsync(string countryCode, decimal nominalReturnPct, int startYear, int endYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Backward-compatible overload defaulting to Bangladesh sovereign economic series ("BD").
    /// </summary>
    Task<RealReturnCalculationResultDto> CalculateRealReturnAsync(decimal nominalReturnPct, int startYear, int endYear, string countryCode = "BD", CancellationToken cancellationToken = default);

    /// <summary>
    /// FEATURE: Date-Range Real Return Calculation (Monthly CPI Index Series)
    /// Evaluates cumulative inflation and purchasing power return over an investment horizon [startDate, endDate].
    /// </summary>
    Task<RealReturnCalculationResultDto> CalculateRealReturnForDateRangeAsync(string countryCode, decimal nominalReturnPct, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// INFLATION: Annual Series Retrieval
    /// Returns ordered chronological annual inflation rates for chart rendering and audits.
    /// </summary>
    Task<List<InflationRateDto>> GetHistoricalRatesAsync(string countryCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// INFLATION: Monthly CPI Observation Retrieval
    /// Returns ordered chronological monthly CPI index levels for audit and provenance verification.
    /// </summary>
    Task<List<CpiIndexObservationDto>> GetCpiObservationsAsync(string countryCode, CancellationToken cancellationToken = default);
}

/// <summary>
/// FEATURE: Inflation-Adjusted Return (LF-28)
/// 
/// PURPOSE:
/// Evaluates whether portfolio investment returns generated true economic purchasing power growth
/// after stripping out the erosive effects of consumer price index (CPI) currency inflation.
/// 
/// DATABASE: Tables — inflation_rates & cpi_index_observations
/// Interacts with the MySQL database via EF Core, utilizing verified BBS/BLS static fallbacks
/// for offline resilience and demonstration determinism.
/// </summary>
public class InflationService : IInflationService
{
    private readonly IApplicationDbContext _context;

    // INFLATION: Documented official annual baseline rates (Bangladesh Bank / BBS and US BLS)
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

    // INFLATION: Official monthly CPI Index series (Base: 2021-22 = 100, General National Index)
    // Sourced from Bangladesh Bureau of Statistics (BBS) Price & Wage Releases through August 2026.
    private static readonly Dictionary<(string, int, int), (decimal Index, string Source, string SourceUrl)> StaticBaselineCpiObservations = new()
    {
        { ("BD", 2024, 9),  (130.6100m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2024, 10), (133.3179m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2024, 11), (132.6561m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2024, 12), (131.2964m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2025, 1),  (131.4872m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2025, 2),  (131.2854m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2025, 3),  (132.9763m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2025, 4),  (133.5048m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2025, 5),  (133.0813m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2025, 6),  (133.8462m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2025, 7),  (136.8319m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2025, 8),  (140.0624m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2025, 9),  (141.5266m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2025, 10), (144.2073m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2025, 11), (143.6473m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2025, 12), (142.4371m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2026, 1),  (142.7712m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2026, 2),  (143.2784m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2026, 3),  (144.5586m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2026, 4),  (145.5748m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2026, 5),  (145.6140m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2026, 6),  (146.1060m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2026, 7),  (148.2145m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") },
        { ("BD", 2026, 8),  (151.6366m, "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)", "https://bbs.gov.bd/site/page/29b379ff-7bac-41d9-b321-e41929bab4a1") }
    };

    public InflationService(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// INFLATION: CPI Data
    /// CALCULATION: Cumulative Inflation (Annual series)
    /// 
    /// Formula:
    /// Cumulative Factor = Product_{y = startYear}^{endYear} (1 + Rate_y / 100)
    /// Cumulative Inflation (%) = (Cumulative Factor - 1.0) * 100
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
    /// INFLATION: Date-Based Cumulative Rate Retrieval (Monthly CPI Index Levels)
    /// Formula: Cumulative Inflation (%) = ((Ending CPI Index / Starting CPI Index) - 1) * 100
    /// </summary>
    public async Task<decimal?> GetCumulativeInflationRateForPeriodAsync(string countryCode, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var result = await CalculateRealReturnForDateRangeAsync(countryCode, 0m, startDate, endDate, cancellationToken);
        return result.InflationRatePercentage;
    }

    /// <summary>
    /// CALCULATION: Real Return (Fisher Equation)
    /// 
    /// Formula:
    /// (1 + r_real) = (1 + r_nominal) / (1 + i)
    /// Real Return (%) = [ ( (1 + Nominal / 100) / (1 + Inflation / 100) ) - 1 ] * 100
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
    /// API: Real Return Calculation Endpoint Backend (Annual series)
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
            result.Status = "Adjusted via official annual CPI inflation series";
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
    /// FEATURE: Date-Range Real Return Calculation (Monthly CPI Index Series)
    /// Evaluates cumulative inflation and purchasing power return over an investment horizon [startDate, endDate].
    /// Uses official monthly Consumer Price Index (CPI) index levels.
    /// Formula: Cumulative Inflation (%) = ((Ending CPI / Starting CPI) - 1) * 100
    /// </summary>
    public async Task<RealReturnCalculationResultDto> CalculateRealReturnForDateRangeAsync(
        string countryCode,
        decimal nominalReturnPct,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var normalizedCountry = string.IsNullOrWhiteSpace(countryCode) ? "BD" : countryCode.Trim().ToUpperInvariant();

        var result = new RealReturnCalculationResultDto
        {
            NominalReturnPercentage = Math.Round(nominalReturnPct, 2),
            CountryCode = normalizedCountry,
            IsDataAvailable = false
        };

        if (startDate > endDate)
        {
            result.Status = "Invalid date range: start date cannot be after end date.";
            return result;
        }

        // 1. Country isolation: Check USD portfolio support
        if (normalizedCountry == "US")
        {
            // Report limitation explicitly rather than using Bangladesh CPI for a US-dollar portfolio
            result.Period = $"{startDate:MMM yyyy} - {endDate:MMM yyyy}";
            result.Status = "Monthly CPI index coverage for United States (USD) is currently unavailable.";
            return result;
        }

        // 2. Load observations from DB, falling back to static baseline for resilience/unit tests
        var dbObservations = await _context.CpiIndexObservations
            .AsNoTracking()
            .Where(o => o.CountryCode == normalizedCountry)
            .OrderBy(o => o.ObservationDate)
            .ToListAsync(cancellationToken);

        var observations = new List<CpiIndexObservation>();
        if (dbObservations.Any())
        {
            observations.AddRange(dbObservations);
        }
        else
        {
            // Fallback to verified static baseline
            var baselineList = StaticBaselineCpiObservations
                .Where(kvp => kvp.Key.Item1 == normalizedCountry)
                .OrderBy(kvp => kvp.Key.Item2).ThenBy(kvp => kvp.Key.Item3)
                .Select((kvp, idx) => new CpiIndexObservation
                {
                    ObservationId = idx + 1,
                    CountryCode = kvp.Key.Item1,
                    ObservationDate = new DateTime(kvp.Key.Item2, kvp.Key.Item3, 1),
                    CpiIndex = kvp.Value.Index,
                    Source = kvp.Value.Source,
                    SourceUrl = kvp.Value.SourceUrl
                });
            observations.AddRange(baselineList);
        }

        if (!observations.Any())
        {
            result.Period = $"{startDate:MMM yyyy} - {endDate:MMM yyyy}";
            result.Status = $"No CPI index observations available for country {normalizedCountry}.";
            return result;
        }

        // 3. Validate non-positive index values explicitly
        if (observations.Any(o => o.CpiIndex <= 0m))
        {
            result.Period = $"{startDate:MMM yyyy} - {endDate:MMM yyyy}";
            result.Status = "Invalid non-positive CPI index observation encountered.";
            return result;
        }

        // 4. Map observations by (Year, Month)
        var obsMap = observations.ToDictionary(
            o => (o.ObservationDate.Year, o.ObservationDate.Month),
            o => o);

        var minDate = observations.Min(o => o.ObservationDate);
        var maxDate = observations.Max(o => o.ObservationDate);

        var startMonth = new DateTime(startDate.Year, startDate.Month, 1);
        var reqEndMonth = new DateTime(endDate.Year, endDate.Month, 1);

        // Check if start period is supported
        if (startMonth > maxDate)
        {
            result.Period = $"{startMonth:MMM yyyy} - {reqEndMonth:MMM yyyy}";
            result.Status = $"Investment start period ({startMonth:MMM yyyy}) is after latest published CPI observation ({maxDate:MMM yyyy}).";
            return result;
        }

        if (startMonth < minDate)
        {
            result.Period = $"{startMonth:MMM yyyy} - {reqEndMonth:MMM yyyy}";
            result.Status = $"CPI observation missing for start period ({startMonth:MMM yyyy}). Earliest available observation is {minDate:MMM yyyy}.";
            return result;
        }

        if (!obsMap.TryGetValue((startMonth.Year, startMonth.Month), out var startObs))
        {
            result.Period = $"{startMonth:MMM yyyy} - {reqEndMonth:MMM yyyy}";
            result.Status = $"CPI observation missing for start month {startMonth:MMM yyyy}.";
            return result;
        }

        // Effective end observation: Use latest available official observation if current calendar month is not yet published
        var effectiveEndMonth = reqEndMonth >= maxDate ? maxDate : reqEndMonth;
        if (!obsMap.TryGetValue((effectiveEndMonth.Year, effectiveEndMonth.Month), out var endObs))
        {
            result.Period = $"{startMonth:MMM yyyy} - {effectiveEndMonth:MMM yyyy}";
            result.Status = $"CPI observation missing for end month {effectiveEndMonth:MMM yyyy}.";
            return result;
        }

        // 5. Verify continuous monthly coverage between start and effective end
        var cursor = startMonth;
        while (cursor <= effectiveEndMonth)
        {
            if (!obsMap.TryGetValue((cursor.Year, cursor.Month), out var stepObs) || stepObs.CpiIndex <= 0m)
            {
                result.Period = $"{startMonth:MMM yyyy} - {effectiveEndMonth:MMM yyyy}";
                result.Status = $"Missing intermediate CPI observation for {cursor:MMM yyyy}.";
                return result;
            }
            cursor = cursor.AddMonths(1);
        }

        // 6. Calculate cumulative inflation using official CPI index levels
        // Formula: Cumulative Inflation (%) = ((Ending CPI Index / Starting CPI Index) - 1) * 100
        var cumulativeInflationPct = ((endObs.CpiIndex / startObs.CpiIndex) - 1.0m) * 100.0m;
        var roundedInflation = Math.Round(cumulativeInflationPct, 2);

        // 7. Calculate real return using Fisher equation
        var realReturnPct = CalculateRealReturn(nominalReturnPct, roundedInflation);

        result.InflationRatePercentage = roundedInflation;
        result.RealReturnPercentage = realReturnPct;
        result.IsDataAvailable = true;
        result.Period = $"{startObs.ObservationDate:MMM yyyy} - {endObs.ObservationDate:MMM yyyy}";
        result.StartingCpiIndex = startObs.CpiIndex;
        result.EndingCpiIndex = endObs.CpiIndex;
        result.ObservationPeriodStart = startObs.ObservationDate.ToString("MMM yyyy");
        result.ObservationPeriodEnd = endObs.ObservationDate.ToString("MMM yyyy");
        result.LatestObservationMonth = maxDate.ToString("MMM yyyy");
        result.Source = endObs.Source;
        result.SourceUrl = endObs.SourceUrl;

        if (reqEndMonth > maxDate)
        {
            result.Status = $"Adjusted via official BBS CPI index (Base 2021-22 = 100) through {endObs.ObservationDate:MMM yyyy} (latest published)";
        }
        else
        {
            result.Status = $"Adjusted via official BBS CPI index (Base 2021-22 = 100) for {result.Period}";
        }

        return result;
    }

    /// <summary>
    /// INFLATION: Annual Series Retrieval
    /// DATABASE: Table — inflation_rates
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

    /// <summary>
    /// INFLATION: Monthly CPI Observation Retrieval
    /// DATABASE: Table — cpi_index_observations
    /// </summary>
    public async Task<List<CpiIndexObservationDto>> GetCpiObservationsAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        var normalizedCountry = string.IsNullOrWhiteSpace(countryCode) ? "BD" : countryCode.Trim().ToUpperInvariant();

        var dbObservations = await _context.CpiIndexObservations
            .AsNoTracking()
            .Where(o => o.CountryCode == normalizedCountry)
            .OrderBy(o => o.ObservationDate)
            .Select(o => new CpiIndexObservationDto
            {
                ObservationId = o.ObservationId,
                CountryCode = o.CountryCode,
                ObservationDate = o.ObservationDate,
                CpiIndex = o.CpiIndex,
                Source = o.Source,
                SourceUrl = o.SourceUrl
            })
            .ToListAsync(cancellationToken);

        if (!dbObservations.Any())
        {
            return StaticBaselineCpiObservations
                .Where(kvp => kvp.Key.Item1 == normalizedCountry)
                .OrderBy(kvp => kvp.Key.Item2).ThenBy(kvp => kvp.Key.Item3)
                .Select((kvp, idx) => new CpiIndexObservationDto
                {
                    ObservationId = idx + 1,
                    CountryCode = kvp.Key.Item1,
                    ObservationDate = new DateTime(kvp.Key.Item2, kvp.Key.Item3, 1),
                    CpiIndex = kvp.Value.Index,
                    Source = kvp.Value.Source,
                    SourceUrl = kvp.Value.SourceUrl
                })
                .ToList();
        }

        return dbObservations;
    }
}
