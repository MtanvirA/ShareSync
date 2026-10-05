using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Analytics;
using ShareSync.Application.DTOs.Reports;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly IApplicationDbContext _context;
    private readonly IReportService _reportService;
    private readonly IDividendService _dividendService;

    public AnalyticsService(
        IApplicationDbContext context,
        IReportService reportService,
        IDividendService dividendService)
    {
        _context = context;
        _reportService = reportService;
        _dividendService = dividendService;
    }

    public async Task<ApiResponse<PortfolioAnalyticsDto>> GetAnalyticsAsync(
        int userId,
        int? portfolioId = null,
        string? period = "ALL",
        CancellationToken cancellationToken = default)
    {
        // 1. Verify User Portfolios and Access Control
        var userPortfolios = await _context.Portfolios
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        if (!userPortfolios.Any())
        {
            var emptyResult = BuildEmptyAnalytics(null, "No Portfolios");
            return ApiResponse<PortfolioAnalyticsDto>.Ok(emptyResult);
        }

        string contextName = "All Portfolios";
        List<int> targetPortfolioIds;

        if (portfolioId.HasValue)
        {
            var target = userPortfolios.FirstOrDefault(p => p.PortfolioId == portfolioId.Value);
            if (target == null)
            {
                // Check if portfolio exists in system to distinguish 404 from 403
                var existsInDb = await _context.Portfolios
                    .AsNoTracking()
                    .AnyAsync(p => p.PortfolioId == portfolioId.Value, cancellationToken);

                if (!existsInDb)
                {
                    throw new NotFoundException("Portfolio", portfolioId.Value);
                }

                throw new AppException("You do not have permission to access this portfolio.", 403);
            }

            contextName = target.PortfolioName;
            targetPortfolioIds = new List<int> { target.PortfolioId };
        }
        else
        {
            targetPortfolioIds = userPortfolios.Select(p => p.PortfolioId).ToList();
        }

        // 2. Reuse ReportService for 100% Financial Consistency (VW_PORTFOLIO_HOLDINGS)
        var holdingsRes = await _reportService.GetHoldingsReportAsync(userId, portfolioId, cancellationToken);
        var holdingsData = holdingsRes.Data ?? new PortfolioHoldingsReportDto();

        var sectorRes = await _reportService.GetCompanySectorReportAsync(userId, portfolioId, cancellationToken);
        var sectorData = sectorRes.Data ?? new CompanySectorReportDto();

        // 3. Core Valuation Metrics
        var totalMarketValue = holdingsData.TotalMarketValue;
        var totalInvested = holdingsData.TotalInvested;
        var totalUnrealizedPL = holdingsData.TotalUnrealizedProfitLoss;
        var unrealizedReturnPct = totalInvested > 0
            ? Math.Round((totalUnrealizedPL / totalInvested) * 100, 2)
            : 0m;

        // 4. Realized Profit/Loss under the Established Weighted-Average Cost Model
        var transactions = await _context.Transactions
            .AsNoTracking()
            .Where(t => targetPortfolioIds.Contains(t.PortfolioId))
            .ToListAsync(cancellationToken);

        decimal totalRealizedPL = 0m;
        var companyTxGroups = transactions.GroupBy(t => t.CompanyId);

        foreach (var group in companyTxGroups)
        {
            var buyTxs = group.Where(t => t.TransactionType == "BUY").ToList();
            var sellTxs = group.Where(t => t.TransactionType == "SELL").ToList();

            if (!sellTxs.Any() || !buyTxs.Any()) continue;

            var totalBuyQty = buyTxs.Sum(t => t.Quantity);
            var totalBuyCost = buyTxs.Sum(t => t.Quantity * t.PricePerShare);
            var weightedAvgCost = totalBuyQty > 0 ? totalBuyCost / totalBuyQty : 0m;

            foreach (var sell in sellTxs)
            {
                totalRealizedPL += (sell.PricePerShare - weightedAvgCost) * sell.Quantity;
            }
        }
        totalRealizedPL = Math.Round(totalRealizedPL, 2);

        // 5. Dividend Income & Yield Contribution
        decimal totalDividends = 0m;
        if (portfolioId.HasValue)
        {
            // Calculate dividend income specifically for this portfolio's companies & shares
            var portfolioTxs = transactions.Where(t => t.PortfolioId == portfolioId.Value).ToList();
            var portfolioCompanyIds = portfolioTxs.Select(t => t.CompanyId).Distinct().ToList();

            if (portfolioCompanyIds.Any())
            {
                var dividends = await _context.Dividends
                    .AsNoTracking()
                    .Where(d => portfolioCompanyIds.Contains(d.CompanyId))
                    .ToListAsync(cancellationToken);

                foreach (var div in dividends)
                {
                    var sharesHeld = portfolioTxs
                        .Where(t => t.CompanyId == div.CompanyId)
                        .Sum(t => t.TransactionType == "BUY" ? t.Quantity : -t.Quantity);

                    if (sharesHeld > 0)
                    {
                        totalDividends += sharesHeld * div.DividendPerShare;
                    }
                }
            }
        }
        else
        {
            var divReportRes = await _reportService.GetDividendIncomeReportAsync(userId, null, null, null, null, cancellationToken);
            totalDividends = divReportRes.Data?.TotalIncome ?? 0m;
        }
        totalDividends = Math.Round(totalDividends, 2);

        var dividendYield = totalMarketValue > 0
            ? Math.Round((totalDividends / totalMarketValue) * 100, 2)
            : 0m;

        // 6. Total Return Calculation
        var totalReturn = Math.Round(totalUnrealizedPL + totalRealizedPL + totalDividends, 2);
        var totalReturnPct = totalInvested > 0
            ? Math.Round((totalReturn / totalInvested) * 100, 2)
            : 0m;

        // 7. Sector Allocation Mapping
        var sectorAllocations = (sectorData.Sectors ?? new List<SectorAllocationItemDto>())
            .Select(s => new AnalyticsSectorDto
            {
                SectorName = s.SectorName,
                MarketValue = s.CurrentMarketValue,
                InvestedValue = s.TotalInvested,
                UnrealizedProfitLoss = s.UnrealizedProfitLoss,
                AllocationPercentage = s.AllocationPercentage,
                HoldingsCount = s.HoldingsCount
            })
            .OrderByDescending(s => s.MarketValue)
            .ToList();

        // 8. Company Allocation Mapping
        var companyAllocations = (sectorData.Companies ?? new List<CompanyInvestmentItemDto>())
            .Select(c =>
            {
                var retPct = c.TotalInvested > 0
                    ? Math.Round((c.UnrealizedProfitLoss / c.TotalInvested) * 100, 2)
                    : 0m;
                var avgPrice = c.SharesHeld > 0
                    ? Math.Round(c.TotalInvested / c.SharesHeld, 2)
                    : 0m;
                var curPrice = c.SharesHeld > 0
                    ? Math.Round(c.CurrentMarketValue / c.SharesHeld, 2)
                    : 0m;

                return new AnalyticsCompanyDto
                {
                    CompanyId = c.CompanyId,
                    TickerSymbol = c.TickerSymbol,
                    CompanyName = c.CompanyName,
                    SectorName = c.SectorName,
                    Shares = c.SharesHeld,
                    AverageBuyPrice = avgPrice,
                    CurrentPrice = curPrice,
                    MarketValue = c.CurrentMarketValue,
                    InvestedValue = c.TotalInvested,
                    UnrealizedProfitLoss = c.UnrealizedProfitLoss,
                    ReturnPercentage = retPct,
                    AllocationPercentage = c.AllocationPercentage
                };
            })
            .OrderByDescending(c => c.MarketValue)
            .ToList();

        // 9. Concentration & Risk Metrics
        decimal top1Weight = companyAllocations.FirstOrDefault()?.AllocationPercentage ?? 0m;
        decimal top3Weight = Math.Round(companyAllocations.Take(3).Sum(c => c.AllocationPercentage), 2);
        decimal top5Weight = Math.Round(companyAllocations.Take(5).Sum(c => c.AllocationPercentage), 2);
        decimal largestSectorWeight = sectorAllocations.FirstOrDefault()?.AllocationPercentage ?? 0m;

        // Herfindahl-Hirschman Index (HHI) = Sum(w_i^2) where w_i is allocation percentage (0 to 100)
        decimal hhi = Math.Round(companyAllocations.Sum(c => c.AllocationPercentage * c.AllocationPercentage), 2);

        // Transparent Diversification Score (0% to 100%):
        // Derived from Herfindahl-Hirschman Index (HHI) where 10,000 is maximum concentration (single stock).
        // Diversification Score = 100 - (HHI / 100), clamped to [0, 100].
        // Higher score indicates lower concentration and broader risk distribution across holdings.
        decimal diversificationScore = 0m;
        if (companyAllocations.Any() && totalMarketValue > 0)
        {
            diversificationScore = Math.Max(0m, Math.Min(100m, Math.Round(100m - (hhi / 100m), 1)));
        }

        string concentrationStatus;
        if (!companyAllocations.Any())
        {
            concentrationStatus = "Empty Portfolio";
        }
        else if (hhi < 1500)
        {
            concentrationStatus = "Diversified";
        }
        else if (hhi <= 2500)
        {
            concentrationStatus = "Moderately Concentrated";
        }
        else
        {
            concentrationStatus = "Highly Concentrated";
        }

        // 10. Top Gainers & Losers
        var topGainers = companyAllocations
            .Where(c => c.ReturnPercentage >= 0)
            .OrderByDescending(c => c.ReturnPercentage)
            .Take(5)
            .Select(c => new AnalyticsPerformanceItemDto
            {
                CompanyId = c.CompanyId,
                TickerSymbol = c.TickerSymbol,
                CompanyName = c.CompanyName,
                CurrentPrice = c.CurrentPrice,
                AverageBuyPrice = c.AverageBuyPrice,
                UnrealizedProfitLoss = c.UnrealizedProfitLoss,
                ReturnPercentage = c.ReturnPercentage,
                MarketValue = c.MarketValue
            })
            .ToList();

        var topLosers = companyAllocations
            .Where(c => c.ReturnPercentage < 0)
            .OrderBy(c => c.ReturnPercentage)
            .Take(5)
            .Select(c => new AnalyticsPerformanceItemDto
            {
                CompanyId = c.CompanyId,
                TickerSymbol = c.TickerSymbol,
                CompanyName = c.CompanyName,
                CurrentPrice = c.CurrentPrice,
                AverageBuyPrice = c.AverageBuyPrice,
                UnrealizedProfitLoss = c.UnrealizedProfitLoss,
                ReturnPercentage = c.ReturnPercentage,
                MarketValue = c.MarketValue
            })
            .ToList();

        // 11. Historical Portfolio Performance Chart
        var perfChart = await BuildHistoricalPerformanceChartAsync(
            targetPortfolioIds,
            period,
            totalMarketValue,
            cancellationToken);

        // 12. Accounting Formulas Dictionary
        var formulas = new Dictionary<string, string>
        {
            { "PortfolioValue", "Sum of (CurrentQuantity * CurrentPrice) for all active holdings." },
            { "InvestedCapital", "Sum of (CurrentQuantity * WeightedAverageBuyPrice) for all active holdings." },
            { "UnrealizedProfitLoss", "PortfolioValue - InvestedCapital" },
            { "RealizedProfitLoss", "Sum of ((SellPrice - WeightedAverageBuyPrice) * QuantitySold) for all executed SELL transactions." },
            { "DividendIncome", "Sum of (SharesHeld * DividendPerShare) for corporate distributions credited to active positions." },
            { "TotalReturn", "UnrealizedProfitLoss + RealizedProfitLoss + DividendIncome" },
            { "TotalReturnPercentage", "(TotalReturn / InvestedCapital) * 100" },
            { "LargestHoldingPercentage", "(Largest Company Position Value / Total Portfolio Value) * 100" },
            { "LargestSectorPercentage", "(Largest Sector Position Value / Total Portfolio Value) * 100" },
            { "DiversificationScore", "Derived from Herfindahl-Hirschman Index: Max(0, Min(100, 100 - (HHI / 100))). Higher score represents broader asset risk distribution (0% = single asset concentration, 100% = perfectly dispersed)." },
            { "HerfindahlIndex", "Sum of squared asset allocation percentages (Sum(w_i^2)). <1500 Diversified, 1500-2500 Moderately Concentrated, >2500 Highly Concentrated." }
        };

        var analyticsDto = new PortfolioAnalyticsDto
        {
            PortfolioId = portfolioId,
            PortfolioName = contextName,
            PortfolioValue = totalMarketValue,
            InvestedCapital = totalInvested,
            UnrealizedProfitLoss = totalUnrealizedPL,
            UnrealizedReturnPercentage = unrealizedReturnPct,
            RealizedProfitLoss = totalRealizedPL,
            DividendIncome = totalDividends,
            DividendYield = dividendYield,
            TotalReturn = totalReturn,
            TotalReturnPercentage = totalReturnPct,
            TopHoldingWeight = top1Weight,
            LargestSectorWeight = largestSectorWeight,
            DiversificationScore = diversificationScore,
            Top3Concentration = top3Weight,
            Top5Concentration = top5Weight,
            HerfindahlIndex = hhi,
            ConcentrationStatus = concentrationStatus,
            TotalHoldingsCount = companyAllocations.Count,
            TotalSectorsCount = sectorAllocations.Count,
            SectorAllocation = sectorAllocations,
            CompanyAllocation = companyAllocations,
            TopGainers = topGainers,
            TopLosers = topLosers,
            HistoricalPerformance = perfChart,
            MetricFormulas = formulas
        };

        return ApiResponse<PortfolioAnalyticsDto>.Ok(analyticsDto);
    }

    private async Task<AnalyticsPerformanceChartDto> BuildHistoricalPerformanceChartAsync(
        List<int> portfolioIds,
        string? period,
        decimal currentLiveValue,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow.Date;
        DateTime? startDate = null;
        var pUpper = (period ?? "ALL").Trim().ToUpperInvariant();

        if (pUpper == "1M")
        {
            startDate = now.AddMonths(-1);
        }
        else if (pUpper == "3M")
        {
            startDate = now.AddMonths(-3);
        }
        else if (pUpper == "6M")
        {
            startDate = now.AddMonths(-6);
        }
        else if (pUpper == "1Y")
        {
            startDate = now.AddYears(-1);
        }

        var query = _context.PortfolioSnapshots
            .AsNoTracking()
            .Where(s => portfolioIds.Contains(s.PortfolioId));

        if (startDate.HasValue)
        {
            query = query.Where(s => s.SnapshotDate >= startDate.Value);
        }

        var snapshots = await query
            .OrderBy(s => s.SnapshotDate)
            .ToListAsync(cancellationToken);

        var chartDto = new AnalyticsPerformanceChartDto { Period = pUpper };

        if (snapshots.Any())
        {
            // If multiple portfolios, group by snapshot date
            var grouped = snapshots
                .GroupBy(s => s.SnapshotDate.Date)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Date = g.Key,
                    TotalValue = Math.Round(g.Sum(s => s.TotalValue), 2)
                })
                .ToList();

            chartDto.Labels = grouped.Select(g => g.Date.ToString("MMM dd")).ToList();
            chartDto.Values = grouped.Select(g => g.TotalValue).ToList();
            chartDto.StartingValue = chartDto.Values.First();
            chartDto.EndingValue = chartDto.Values.Last();
            chartDto.NetChange = chartDto.EndingValue - chartDto.StartingValue;
            chartDto.NetChangePercentage = chartDto.StartingValue > 0
                ? Math.Round((chartDto.NetChange / chartDto.StartingValue) * 100, 2)
                : 0m;
        }
        else
        {
            // If no snapshots recorded yet, represent current live valuation
            chartDto.Labels.Add(now.ToString("MMM dd"));
            chartDto.Values.Add(currentLiveValue);
            chartDto.StartingValue = currentLiveValue;
            chartDto.EndingValue = currentLiveValue;
            chartDto.NetChange = 0m;
            chartDto.NetChangePercentage = 0m;
        }

        return chartDto;
    }

    private static PortfolioAnalyticsDto BuildEmptyAnalytics(int? portfolioId, string name)
    {
        return new PortfolioAnalyticsDto
        {
            PortfolioId = portfolioId,
            PortfolioName = name,
            PortfolioValue = 0m,
            InvestedCapital = 0m,
            UnrealizedProfitLoss = 0m,
            UnrealizedReturnPercentage = 0m,
            RealizedProfitLoss = 0m,
            DividendIncome = 0m,
            DividendYield = 0m,
            TotalReturn = 0m,
            TotalReturnPercentage = 0m,
            TopHoldingWeight = 0m,
            LargestSectorWeight = 0m,
            DiversificationScore = 0m,
            Top3Concentration = 0m,
            Top5Concentration = 0m,
            HerfindahlIndex = 0m,
            ConcentrationStatus = "Empty Portfolio",
            TotalHoldingsCount = 0,
            TotalSectorsCount = 0,
            SectorAllocation = new List<AnalyticsSectorDto>(),
            CompanyAllocation = new List<AnalyticsCompanyDto>(),
            TopGainers = new List<AnalyticsPerformanceItemDto>(),
            TopLosers = new List<AnalyticsPerformanceItemDto>(),
            HistoricalPerformance = new AnalyticsPerformanceChartDto
            {
                Period = "ALL",
                StartingValue = 0m,
                EndingValue = 0m,
                NetChange = 0m,
                NetChangePercentage = 0m,
                Labels = new List<string> { DateTime.UtcNow.ToString("MMM dd") },
                Values = new List<decimal> { 0m }
            },
            MetricFormulas = new Dictionary<string, string>
            {
                { "PortfolioValue", "Sum of (CurrentQuantity * CurrentPrice) for all active holdings." },
                { "InvestedCapital", "Sum of (CurrentQuantity * WeightedAverageBuyPrice) for all active holdings." },
                { "UnrealizedProfitLoss", "PortfolioValue - InvestedCapital" },
                { "RealizedProfitLoss", "Sum of ((SellPrice - WeightedAverageBuyPrice) * QuantitySold) for all executed SELL transactions." },
                { "DividendIncome", "Sum of (SharesHeld * DividendPerShare) for corporate distributions credited to active positions." },
                { "TotalReturn", "UnrealizedProfitLoss + RealizedProfitLoss + DividendIncome" },
                { "LargestHoldingPercentage", "(Largest Company Position Value / Total Portfolio Value) * 100" },
                { "LargestSectorPercentage", "(Largest Sector Position Value / Total Portfolio Value) * 100" },
                { "DiversificationScore", "Derived from Herfindahl-Hirschman Index: Max(0, Min(100, 100 - (HHI / 100)))." }
            }
        };
    }
}
