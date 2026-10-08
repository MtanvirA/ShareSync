using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.DTOs.Benchmarks;
using ShareSync.Application.Interfaces;

namespace ShareSync.Application.Services;

/// <summary>
/// FEATURE: Benchmark Comparison (LF-30)
/// 
/// BENCHMARK: Historical Data
/// Compares investor portfolio performance against recognized Dhaka Stock Exchange (DSE) market indices:
/// - DSEX (Broad Market Free-Float Index)
/// - DS30 (Blue-Chip 30 Leading Investable Companies)
/// - DSES / DSEG (Shariah Compliance Equities Index)
/// 
/// BENCHMARK: Active Return
/// CALCULATION: Benchmark Outperformance
/// Formula: Active Return / Outperformance (%) = Portfolio Return (%) - Benchmark Return (%)
/// 
/// BUSINESS RULE: Date Alignment and Data Integrity
/// Compares returns only over strictly overlapping historical dates.
/// Requires at least two distinct valuation points. If DB historical records are unseeded,
/// safely falls back to calibrated reference series with transparent UI disclosure (IsFallback = true).
/// 
/// DATABASE: Table — benchmark_price_history
/// </summary>
public class BenchmarkService : IBenchmarkService
{
    private readonly IApplicationDbContext _context;
    private readonly IBenchmarkDataProvider _dataProvider;

    public BenchmarkService(IApplicationDbContext context, IBenchmarkDataProvider dataProvider)
    {
        _context = context;
        _dataProvider = dataProvider;
    }

    /// <summary>
    /// BENCHMARK: Index Catalog Retrieval
    /// DATABASE: Table — benchmark_price_history
    /// 
    /// Aggregates distinct benchmark series from MySQL, extracting observation counts,
    /// earliest baseline closing prices, and latest index points. Falls back to static definitions if empty.
    /// </summary>
    public async Task<List<BenchmarkDto>> GetAvailableBenchmarksAsync(CancellationToken cancellationToken = default)
    {
        var dbBenchmarks = await _context.BenchmarkPriceHistories
            .AsNoTracking()
            .GroupBy(b => b.BenchmarkCode)
            .Select(g => new
            {
                Code = g.Key,
                Name = g.Max(b => b.BenchmarkName),
                Source = g.Max(b => b.Source),
                LatestDate = g.Max(b => b.TradingDate),
                CurrentValue = g.OrderByDescending(b => b.TradingDate).Select(b => b.CloseValue).FirstOrDefault(),
                BaseValue = g.OrderBy(b => b.TradingDate).Select(b => b.CloseValue).FirstOrDefault(),
                ObservationCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        if (dbBenchmarks.Any())
        {
            var result = new List<BenchmarkDto>();
            foreach (var dbB in dbBenchmarks.OrderBy(b => b.Code))
            {
                var staticDef = _dataProvider.GetBenchmark(dbB.Code);
                result.Add(new BenchmarkDto
                {
                    BenchmarkCode = dbB.Code,
                    Name = !string.IsNullOrWhiteSpace(dbB.Name) ? dbB.Name : (staticDef?.Name ?? dbB.Code),
                    Description = staticDef?.Description ?? $"Database-backed index ({dbB.ObservationCount} observations, latest {dbB.LatestDate:yyyy-MM-dd}).",
                    Provider = !string.IsNullOrWhiteSpace(dbB.Source) ? dbB.Source : (staticDef?.Provider ?? "Dhaka Stock Exchange"),
                    CurrentValue = dbB.CurrentValue,
                    BaseValue = dbB.BaseValue > 0 ? dbB.BaseValue : (staticDef?.BaseValue ?? 1000m)
                });
            }
            return result;
        }

        return _dataProvider.GetAvailableBenchmarks();
    }

    /// <summary>
    /// FEATURE: Benchmark Comparison (LF-30)
    /// BENCHMARK: Active Return
    /// CALCULATION: Benchmark Outperformance
    /// DATA QUALITY: Date Alignment
    /// 
    /// Formula:
    /// Portfolio Return (%) = ((Portfolio Ending Value - Portfolio Starting Value) / Portfolio Starting Value) * 100
    /// Benchmark Return (%) = ((Benchmark Ending Value - Benchmark Starting Value) / Benchmark Starting Value) * 100
    /// Active Return / Outperformance (%) = Portfolio Return (%) - Benchmark Return (%)
    /// 
    /// PURPOSE:
    /// Computes period-aligned returns for the user's portfolio against DSE indices.
    /// Strictly limits comparison to the overlapping calendar intersection of portfolio snapshots and index data.
    /// 
    /// INPUT:
    /// request: Contains optional PortfolioId, BenchmarkCode ("DSEX", "DS30", "DSES"), and Period ("1M", "3M", "6M", "1Y", "ALL").
    /// userId: Authenticated user ID for ownership security isolation.
    /// 
    /// OUTPUT:
    /// BenchmarkComparisonDto: Period-by-period aligned value arrays, return percentages, and outperformance flag.
    /// 
    /// BUSINESS RULE: Minimum Data Sufficiency
    /// Requires at least 2 distinct valuation snapshot points within the requested window.
    /// Will not interpolate or extrapolate hypothetical points.
    /// </summary>
    public async Task<BenchmarkComparisonDto> ComparePortfolioAsync(
        BenchmarkComparisonRequestDto request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        request ??= new BenchmarkComparisonRequestDto();

        // 1. Resolve Benchmark Definition & Validate Benchmark Code
        var benchmarkCode = string.IsNullOrWhiteSpace(request.BenchmarkCode) ? "DSEX" : request.BenchmarkCode.Trim().ToUpperInvariant();
        
        // Check DB for this benchmark or recognized aliases
        var hasDbData = await _context.BenchmarkPriceHistories
            .AsNoTracking()
            .AnyAsync(b => b.BenchmarkCode == benchmarkCode, cancellationToken);

        string effectiveCode = benchmarkCode;
        if (!hasDbData && benchmarkCode == "DSEG")
        {
            hasDbData = await _context.BenchmarkPriceHistories
                .AsNoTracking()
                .AnyAsync(b => b.BenchmarkCode == "DSES", cancellationToken);
            if (hasDbData) effectiveCode = "DSES";
        }
        else if (!hasDbData && benchmarkCode == "DSES")
        {
            hasDbData = await _context.BenchmarkPriceHistories
                .AsNoTracking()
                .AnyAsync(b => b.BenchmarkCode == "DSEG", cancellationToken);
            if (hasDbData) effectiveCode = "DSEG";
        }

        var benchmarkDef = _dataProvider.GetBenchmark(effectiveCode) ?? _dataProvider.GetBenchmark(benchmarkCode);

        if (!hasDbData && benchmarkDef == null)
        {
            throw new ValidationException($"Benchmark '{request.BenchmarkCode}' is not recognized or supported. Supported benchmarks include DSEX, DS30, and DSES.");
        }

        var benchmarkName = benchmarkDef?.Name ?? $"{benchmarkCode} Benchmark";
        var benchmarkSource = benchmarkDef?.Provider ?? "Dhaka Stock Exchange";

        // 2. Validate Portfolio Access & Scope
        List<int> portfolioIds;
        string portfolioName;

        if (request.PortfolioId.HasValue)
        {
            var portfolio = await _context.Portfolios
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PortfolioId == request.PortfolioId.Value, cancellationToken);

            if (portfolio == null)
            {
                throw new NotFoundException($"Portfolio with ID {request.PortfolioId.Value} was not found.");
            }

            if (portfolio.UserId != userId)
            {
                throw new AppException("You do not have permission to access this portfolio.", 403);
            }

            portfolioIds = new List<int> { portfolio.PortfolioId };
            portfolioName = portfolio.PortfolioName;
        }
        else
        {
            var userPortfolios = await _context.Portfolios
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .ToListAsync(cancellationToken);

            if (!userPortfolios.Any())
            {
                return new BenchmarkComparisonDto
                {
                    PortfolioId = null,
                    PortfolioName = "No Portfolios",
                    BenchmarkCode = benchmarkCode,
                    BenchmarkName = benchmarkName,
                    BenchmarkSource = benchmarkSource,
                    Period = request.Period,
                    HasSufficientData = false,
                    Message = "No portfolios found for user. Create a portfolio and record BUY transactions to generate performance snapshots.",
                    AvailablePeriods = new List<string>()
                };
            }

            portfolioIds = userPortfolios.Select(p => p.PortfolioId).ToList();
            portfolioName = "Consolidated Portfolio (All)";
        }

        // 3. Retrieve Snapshots
        var allSnapshots = await _context.PortfolioSnapshots
            .AsNoTracking()
            .Where(s => portfolioIds.Contains(s.PortfolioId))
            .OrderBy(s => s.SnapshotDate)
            .ToListAsync(cancellationToken);

        // Group across multiple portfolios by distinct date
        var groupedSnapshots = allSnapshots
            .GroupBy(s => s.SnapshotDate.Date)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Date = g.Key,
                TotalValue = Math.Round(g.Sum(s => s.TotalValue), 2)
            })
            .ToList();

        // 4. Insufficient Data Check (At least 2 portfolio snapshots required)
        if (groupedSnapshots.Count < 2)
        {
            return new BenchmarkComparisonDto
            {
                PortfolioId = request.PortfolioId,
                PortfolioName = portfolioName,
                BenchmarkCode = benchmarkCode,
                BenchmarkName = benchmarkName,
                BenchmarkSource = benchmarkSource,
                Period = request.Period,
                HasSufficientData = false,
                Message = "Insufficient historical snapshot data. At least two distinct valuation snapshot points are required to compute comparative returns without interpolating unverified data.",
                AvailablePeriods = new List<string>()
            };
        }

        var earliestDate = groupedSnapshots.First().Date;
        var latestDate = groupedSnapshots.Last().Date;
        var totalSpanDays = (latestDate - earliestDate).TotalDays;

        var availablePeriods = new List<string> { "ALL" };
        if (totalSpanDays >= 25) availablePeriods.Add("1M");
        if (totalSpanDays >= 75) availablePeriods.Add("3M");
        if (totalSpanDays >= 160) availablePeriods.Add("6M");
        if (totalSpanDays >= 320) availablePeriods.Add("1Y");

        var requestedPeriod = (request.Period ?? "ALL").Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(requestedPeriod)) requestedPeriod = "ALL";

        if (requestedPeriod != "ALL" && !availablePeriods.Contains(requestedPeriod))
        {
            return new BenchmarkComparisonDto
            {
                PortfolioId = request.PortfolioId,
                PortfolioName = portfolioName,
                BenchmarkCode = benchmarkCode,
                BenchmarkName = benchmarkName,
                BenchmarkSource = benchmarkSource,
                Period = requestedPeriod,
                HasSufficientData = false,
                Message = $"Insufficient historical snapshot data for period '{requestedPeriod}'. Your portfolio snapshot history spans {Math.Round(totalSpanDays)} days. Only periods with sufficient real data points are shown.",
                AvailablePeriods = availablePeriods
            };
        }

        // 5. Filter Snapshots by Period
        var now = DateTime.UtcNow.Date;
        DateTime cutoff = requestedPeriod switch
        {
            "1M" => now.AddMonths(-1),
            "3M" => now.AddMonths(-3),
            "6M" => now.AddMonths(-6),
            "1Y" => now.AddYears(-1),
            _ => DateTime.MinValue
        };

        var periodSnapshots = groupedSnapshots.Where(s => s.Date >= cutoff).ToList();

        if (periodSnapshots.Count < 2)
        {
            return new BenchmarkComparisonDto
            {
                PortfolioId = request.PortfolioId,
                PortfolioName = portfolioName,
                BenchmarkCode = benchmarkCode,
                BenchmarkName = benchmarkName,
                BenchmarkSource = benchmarkSource,
                Period = requestedPeriod,
                HasSufficientData = false,
                Message = $"Insufficient snapshot data points within period '{requestedPeriod}'. A minimum of two distinct valuation dates are required.",
                AvailablePeriods = availablePeriods
            };
        }

        // 6. LF-30 Database-Backed Benchmark History Query & Date Alignment
        var dbBenchmarkHistory = await _context.BenchmarkPriceHistories
            .AsNoTracking()
            .Where(b => b.BenchmarkCode == effectiveCode)
            .OrderBy(b => b.TradingDate)
            .ToListAsync(cancellationToken);

        if (dbBenchmarkHistory.Any())
        {
            var benchMinDate = dbBenchmarkHistory.First().TradingDate.Date;
            var benchMaxDate = dbBenchmarkHistory.Last().TradingDate.Date;

            // Date Alignment Rule: Compare only over overlapping dates. Do not compare mismatched periods.
            var overlappingSnapshots = periodSnapshots
                .Where(s => s.Date >= benchMinDate && s.Date <= benchMaxDate)
                .ToList();

            if (overlappingSnapshots.Count < 2)
            {
                return new BenchmarkComparisonDto
                {
                    PortfolioId = request.PortfolioId,
                    PortfolioName = portfolioName,
                    BenchmarkCode = benchmarkCode,
                    BenchmarkName = benchmarkName,
                    BenchmarkSource = $"Database: {dbBenchmarkHistory.First().Source}",
                    Period = requestedPeriod,
                    HasSufficientData = false,
                    Message = $"No sufficient overlapping date alignment for period '{requestedPeriod}'. Portfolio snapshots span {periodSnapshots.First().Date:yyyy-MM-dd} to {periodSnapshots.Last().Date:yyyy-MM-dd}, whereas benchmark history ({effectiveCode}) in the database covers {benchMinDate:yyyy-MM-dd} to {benchMaxDate:yyyy-MM-dd}. At least two overlapping dates are required.",
                    AvailablePeriods = availablePeriods
                };
            }

            // Pair each portfolio snapshot date with the date-aligned benchmark close value
            // (If non-trading day e.g. weekend, align to nearest preceding trading day)
            var alignedPairs = new List<(DateTime Date, decimal PortValue, decimal BenchValue)>();
            foreach (var s in overlappingSnapshots)
            {
                var exact = dbBenchmarkHistory.FirstOrDefault(b => b.TradingDate.Date == s.Date);
                var matched = exact ?? dbBenchmarkHistory.Where(b => b.TradingDate.Date <= s.Date).LastOrDefault();

                if (matched != null)
                {
                    alignedPairs.Add((s.Date, s.TotalValue, matched.CloseValue));
                }
            }

            if (alignedPairs.Count < 2)
            {
                return new BenchmarkComparisonDto
                {
                    PortfolioId = request.PortfolioId,
                    PortfolioName = portfolioName,
                    BenchmarkCode = benchmarkCode,
                    BenchmarkName = benchmarkName,
                    BenchmarkSource = $"Database: {dbBenchmarkHistory.First().Source}",
                    Period = requestedPeriod,
                    HasSufficientData = false,
                    Message = $"Could not align at least two benchmark trading dates for portfolio snapshots in period '{requestedPeriod}'.",
                    AvailablePeriods = availablePeriods
                };
            }

            var firstPair = alignedPairs.First();
            var lastPair = alignedPairs.Last();

            var portStartVal = firstPair.PortValue;
            var portEndVal = lastPair.PortValue;
            var portReturn = portStartVal > 0
                ? Math.Round(((portEndVal - portStartVal) / portStartVal) * 100, 2)
                : 0m;

            var benchStartVal = firstPair.BenchValue;
            var benchEndVal = lastPair.BenchValue;
            var benchReturn = benchStartVal > 0
                ? Math.Round(((benchEndVal - benchStartVal) / benchStartVal) * 100, 2)
                : 0m;

            var diff = Math.Round(portReturn - benchReturn, 2);
            var isOutperforming = diff >= 0;

            var labels = new List<string>();
            var portfolioReturns = new List<decimal>();
            var benchmarkReturns = new List<decimal>();
            var portfolioValues = new List<decimal>();
            var benchmarkValues = new List<decimal>();

            foreach (var pair in alignedPairs)
            {
                labels.Add(pair.Date.ToString("MMM dd"));
                portfolioValues.Add(pair.PortValue);

                var pPct = portStartVal > 0
                    ? Math.Round(((pair.PortValue - portStartVal) / portStartVal) * 100, 2)
                    : 0m;
                portfolioReturns.Add(pPct);

                benchmarkValues.Add(pair.BenchValue);
                var bPct = benchStartVal > 0
                    ? Math.Round(((pair.BenchValue - benchStartVal) / benchStartVal) * 100, 2)
                    : 0m;
                benchmarkReturns.Add(bPct);
            }

            return new BenchmarkComparisonDto
            {
                PortfolioId = request.PortfolioId,
                PortfolioName = portfolioName,
                BenchmarkCode = benchmarkCode,
                BenchmarkName = benchmarkName,
                BenchmarkSource = $"Database Historical Series ({dbBenchmarkHistory.First().Source})",
                DataLimitationNotice = "Date-aligned database benchmark records over strictly overlapping periods. Historical market series only; does not include corporate action adjustments.",
                Period = requestedPeriod,
                HasSufficientData = true,
                IsFallback = false,
                AvailablePeriods = availablePeriods,
                PortfolioStartingValue = portStartVal,
                PortfolioEndingValue = portEndVal,
                PortfolioReturnPercentage = portReturn,
                BenchmarkStartingValue = benchStartVal,
                BenchmarkEndingValue = benchEndVal,
                BenchmarkReturnPercentage = benchReturn,
                OutperformancePercentage = diff,
                IsOutperforming = isOutperforming,
                Labels = labels,
                PortfolioReturns = portfolioReturns,
                BenchmarkReturns = benchmarkReturns,
                PortfolioValues = portfolioValues,
                BenchmarkValues = benchmarkValues
            };
        }

        // 7. Fallback to BenchmarkDataProvider (for in-memory unit tests / unseeded environments)
        var firstSnapshot = periodSnapshots.First();
        var lastSnapshot = periodSnapshots.Last();

        var fallbackPortStart = firstSnapshot.TotalValue;
        var fallbackPortEnd = lastSnapshot.TotalValue;
        var fallbackPortReturn = fallbackPortStart > 0
            ? Math.Round(((fallbackPortEnd - fallbackPortStart) / fallbackPortStart) * 100, 2)
            : 0m;

        var fallbackBenchStart = _dataProvider.GetBenchmarkValue(benchmarkCode, firstSnapshot.Date);
        var fallbackBenchEnd = _dataProvider.GetBenchmarkValue(benchmarkCode, lastSnapshot.Date);
        var fallbackBenchReturn = fallbackBenchStart > 0
            ? Math.Round(((fallbackBenchEnd - fallbackBenchStart) / fallbackBenchStart) * 100, 2)
            : 0m;

        var fallbackDiff = Math.Round(fallbackPortReturn - fallbackBenchReturn, 2);
        var fallbackOutperforming = fallbackDiff >= 0;

        var fallbackLabels = new List<string>();
        var fallbackPortReturns = new List<decimal>();
        var fallbackBenchReturns = new List<decimal>();
        var fallbackPortValues = new List<decimal>();
        var fallbackBenchValues = new List<decimal>();

        foreach (var s in periodSnapshots)
        {
            fallbackLabels.Add(s.Date.ToString("MMM dd"));
            fallbackPortValues.Add(s.TotalValue);

            var pPct = fallbackPortStart > 0
                ? Math.Round(((s.TotalValue - fallbackPortStart) / fallbackPortStart) * 100, 2)
                : 0m;
            fallbackPortReturns.Add(pPct);

            var bVal = _dataProvider.GetBenchmarkValue(benchmarkCode, s.Date);
            fallbackBenchValues.Add(bVal);

            var bPct = fallbackBenchStart > 0
                ? Math.Round(((bVal - fallbackBenchStart) / fallbackBenchStart) * 100, 2)
                : 0m;
            fallbackBenchReturns.Add(bPct);
        }

        return new BenchmarkComparisonDto
        {
            PortfolioId = request.PortfolioId,
            PortfolioName = portfolioName,
            BenchmarkCode = benchmarkCode,
            BenchmarkName = benchmarkName,
            BenchmarkSource = "Static Calibrated Model (Fallback)",
            DataLimitationNotice = "Calibrated reference benchmark model used because real historical database observations are unavailable for the requested period. Not an actual historical market observation.",
            Period = requestedPeriod,
            HasSufficientData = true,
            IsFallback = true,
            AvailablePeriods = availablePeriods,
            PortfolioStartingValue = fallbackPortStart,
            PortfolioEndingValue = fallbackPortEnd,
            PortfolioReturnPercentage = fallbackPortReturn,
            BenchmarkStartingValue = fallbackBenchStart,
            BenchmarkEndingValue = fallbackBenchEnd,
            BenchmarkReturnPercentage = fallbackBenchReturn,
            OutperformancePercentage = fallbackDiff,
            IsOutperforming = fallbackOutperforming,
            Labels = fallbackLabels,
            PortfolioReturns = fallbackPortReturns,
            BenchmarkReturns = fallbackBenchReturns,
            PortfolioValues = fallbackPortValues,
            BenchmarkValues = fallbackBenchValues
        };
    }
}
