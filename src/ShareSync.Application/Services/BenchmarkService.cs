using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.DTOs.Benchmarks;
using ShareSync.Application.Interfaces;

namespace ShareSync.Application.Services;

public class BenchmarkService : IBenchmarkService
{
    private readonly IApplicationDbContext _context;
    private readonly IBenchmarkDataProvider _dataProvider;

    public BenchmarkService(IApplicationDbContext context, IBenchmarkDataProvider dataProvider)
    {
        _context = context;
        _dataProvider = dataProvider;
    }

    public Task<List<BenchmarkDto>> GetAvailableBenchmarksAsync(CancellationToken cancellationToken = default)
    {
        var list = _dataProvider.GetAvailableBenchmarks();
        return Task.FromResult(list);
    }

    public async Task<BenchmarkComparisonDto> ComparePortfolioAsync(
        BenchmarkComparisonRequestDto request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        request ??= new BenchmarkComparisonRequestDto();

        // 1. Resolve Benchmark Definition
        var benchmarkCode = string.IsNullOrWhiteSpace(request.BenchmarkCode) ? "DSEX" : request.BenchmarkCode.Trim().ToUpperInvariant();
        var benchmark = _dataProvider.GetBenchmark(benchmarkCode) ?? _dataProvider.GetBenchmark("DSEX")!;

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
                    BenchmarkCode = benchmark.BenchmarkCode,
                    BenchmarkName = benchmark.Name,
                    BenchmarkSource = benchmark.Provider,
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

        // 4. Insufficient Data Check
        if (groupedSnapshots.Count < 2)
        {
            return new BenchmarkComparisonDto
            {
                PortfolioId = request.PortfolioId,
                PortfolioName = portfolioName,
                BenchmarkCode = benchmark.BenchmarkCode,
                BenchmarkName = benchmark.Name,
                BenchmarkSource = benchmark.Provider,
                Period = request.Period,
                HasSufficientData = false,
                Message = "Insufficient historical snapshot data. At least two distinct valuation snapshot points are required to compute comparative returns without interpolating unverified data.",
                AvailablePeriods = new List<string>()
            };
        }

        var earliestDate = groupedSnapshots.First().Date;
        var latestDate = groupedSnapshots.Last().Date;
        var totalSpanDays = (latestDate - earliestDate).TotalDays;

        // Determine which periods legitimately have sufficient historical coverage
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
                BenchmarkCode = benchmark.BenchmarkCode,
                BenchmarkName = benchmark.Name,
                BenchmarkSource = benchmark.Provider,
                Period = requestedPeriod,
                HasSufficientData = false,
                Message = $"Insufficient historical snapshot data for period '{requestedPeriod}'. Your portfolio snapshot history spans {Math.Round(totalSpanDays)} days. Only periods with sufficient real data points are shown.",
                AvailablePeriods = availablePeriods
            };
        }

        // 5. Filter Snapshots by Period
        var now = DateTime.UtcNow.Date;
        DateTime cutoff;

        if (requestedPeriod == "1M")
        {
            cutoff = now.AddMonths(-1);
        }
        else if (requestedPeriod == "3M")
        {
            cutoff = now.AddMonths(-3);
        }
        else if (requestedPeriod == "6M")
        {
            cutoff = now.AddMonths(-6);
        }
        else if (requestedPeriod == "1Y")
        {
            cutoff = now.AddYears(-1);
        }
        else
        {
            cutoff = DateTime.MinValue;
        }

        var periodSnapshots = groupedSnapshots.Where(s => s.Date >= cutoff).ToList();

        if (periodSnapshots.Count < 2)
        {
            return new BenchmarkComparisonDto
            {
                PortfolioId = request.PortfolioId,
                PortfolioName = portfolioName,
                BenchmarkCode = benchmark.BenchmarkCode,
                BenchmarkName = benchmark.Name,
                BenchmarkSource = benchmark.Provider,
                Period = requestedPeriod,
                HasSufficientData = false,
                Message = $"Insufficient snapshot data points within period '{requestedPeriod}'. A minimum of two distinct valuation dates are required.",
                AvailablePeriods = availablePeriods
            };
        }

        // 6. Calculate Returns & Difference
        var firstSnapshot = periodSnapshots.First();
        var lastSnapshot = periodSnapshots.Last();

        var portStartVal = firstSnapshot.TotalValue;
        var portEndVal = lastSnapshot.TotalValue;
        var portReturn = portStartVal > 0
            ? Math.Round(((portEndVal - portStartVal) / portStartVal) * 100, 2)
            : 0m;

        var benchStartVal = _dataProvider.GetBenchmarkValue(benchmark.BenchmarkCode, firstSnapshot.Date);
        var benchEndVal = _dataProvider.GetBenchmarkValue(benchmark.BenchmarkCode, lastSnapshot.Date);
        var benchReturn = benchStartVal > 0
            ? Math.Round(((benchEndVal - benchStartVal) / benchStartVal) * 100, 2)
            : 0m;

        var diff = Math.Round(portReturn - benchReturn, 2);
        var isOutperforming = diff >= 0;

        // 7. Generate Time-Series for Chart.js
        var labels = new List<string>();
        var portfolioReturns = new List<decimal>();
        var benchmarkReturns = new List<decimal>();
        var portfolioValues = new List<decimal>();
        var benchmarkValues = new List<decimal>();

        foreach (var s in periodSnapshots)
        {
            labels.Add(s.Date.ToString("MMM dd"));
            portfolioValues.Add(s.TotalValue);

            var pPct = portStartVal > 0
                ? Math.Round(((s.TotalValue - portStartVal) / portStartVal) * 100, 2)
                : 0m;
            portfolioReturns.Add(pPct);

            var bVal = _dataProvider.GetBenchmarkValue(benchmark.BenchmarkCode, s.Date);
            benchmarkValues.Add(bVal);

            var bPct = benchStartVal > 0
                ? Math.Round(((bVal - benchStartVal) / benchStartVal) * 100, 2)
                : 0m;
            benchmarkReturns.Add(bPct);
        }

        return new BenchmarkComparisonDto
        {
            PortfolioId = request.PortfolioId,
            PortfolioName = portfolioName,
            BenchmarkCode = benchmark.BenchmarkCode,
            BenchmarkName = benchmark.Name,
            BenchmarkSource = benchmark.Provider,
            DataLimitationNotice = "Controlled reference benchmark index for performance attribution and comparison. Not a live market execution feed.",
            Period = requestedPeriod,
            HasSufficientData = true,
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
}
