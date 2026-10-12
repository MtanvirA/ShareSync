using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Dashboard;
using ShareSync.Application.Interfaces;

namespace ShareSync.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _context;
    private readonly IReportService _reportService;

    public DashboardService(IApplicationDbContext context, IReportService reportService)
    {
        _context = context;
        _reportService = reportService;
    }

    public async Task<ApiResponse<DashboardResponseDto>> GetDashboardDataAsync(
        int userId,
        int? portfolioId = null,
        string? period = "6",
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch user's profile info
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        var userName = user?.Name ?? "Investor";

        // 2. Fetch User's Portfolios
        var userPortfolios = await _context.Portfolios
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        var portfoliosCount = userPortfolios.Count;
        var userPortfolioIds = userPortfolios
            .Where(p => !portfolioId.HasValue || p.PortfolioId == portfolioId.Value)
            .Select(p => p.PortfolioId)
            .ToList();

        // 3. Re-use exact holdings calculations from Reports
        var holdingsRes = await _reportService.GetHoldingsReportAsync(userId, portfolioId, cancellationToken);
        var holdingsData = holdingsRes.Data ?? new DTOs.Reports.PortfolioHoldingsReportDto();

        // 4. Re-use exact dividend calculations from Reports
        var dividendRes = await _reportService.GetDividendIncomeReportAsync(userId, null, null, null, null, cancellationToken);
        var dividendData = dividendRes.Data ?? new DTOs.Reports.DividendIncomeReportDto();

        // 5. Build Summary
        var reportSummaryRes = await _reportService.GetReportSummaryAsync(userId, portfolioId, cancellationToken);
        var repSummary = reportSummaryRes.Data;

        var summary = new DashboardSummaryDto
        {
            TotalPortfolioValue = holdingsData.TotalMarketValue,
            TotalInvested = holdingsData.TotalInvested,
            TotalUnrealizedProfitLoss = holdingsData.TotalUnrealizedProfitLoss,
            TotalRealizedProfitLoss = repSummary?.TotalRealizedProfitLoss ?? 0m,
            TotalFees = repSummary?.TotalFees ?? 0m,
            NetCashFlow = repSummary?.NetCashFlow ?? 0m,
            ProfitLossPercentage = holdingsData.TotalProfitLossPercentage,
            TotalDividendIncome = dividendData.TotalIncome,
            PortfoliosCount = portfoliosCount,
            HoldingsCount = holdingsData.HoldingsCount
        };

        // 6. Top Holdings (top 5 by current market value)
        var topHoldings = holdingsData.Holdings
            .OrderByDescending(h => h.CurrentMarketValue)
            .Take(5)
            .Select(h =>
            {
                var allocPct = holdingsData.TotalMarketValue > 0
                    ? Math.Round((h.CurrentMarketValue / holdingsData.TotalMarketValue) * 100, 2)
                    : 0m;

                return new DashboardTopHoldingDto
                {
                    CompanyId = h.CompanyId,
                    CompanyName = h.CompanyName,
                    TickerSymbol = h.TickerSymbol,
                    Shares = h.CurrentQuantity,
                    AverageBuyPrice = h.WeightedAverageBuyPrice,
                    CurrentPrice = h.CurrentMarketPrice,
                    MarketValue = h.CurrentMarketValue,
                    UnrealizedProfitLoss = h.UnrealizedProfitLoss,
                    ReturnPercentage = h.ProfitLossPercentage,
                    AllocationPercentage = allocPct
                };
            }).ToList();

        // 7. Recent Transactions (latest 5 across user's portfolios)
        var recentTransactions = new List<DashboardRecentTransactionDto>();
        if (userPortfolioIds.Any())
        {
            var rawRecent = await _context.Transactions
                .AsNoTracking()
                .Include(t => t.Company)
                .Include(t => t.Portfolio)
                .Where(t => userPortfolioIds.Contains(t.PortfolioId))
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.TransactionId)
                .Take(5)
                .ToListAsync(cancellationToken);

            var sellPairs = rawRecent
                .Where(t => t.TransactionType == "SELL")
                .Select(t => (t.PortfolioId, t.CompanyId))
                .Distinct()
                .ToList();

            var portIds = sellPairs.Select(p => p.PortfolioId).Distinct().ToList();
            var compIds = sellPairs.Select(p => p.CompanyId).Distinct().ToList();

            var allBuys = await _context.Transactions
                .AsNoTracking()
                .Where(t => portIds.Contains(t.PortfolioId) && compIds.Contains(t.CompanyId) && t.TransactionType == "BUY")
                .ToListAsync(cancellationToken);

            var avgCostMap = allBuys
                .GroupBy(b => (b.PortfolioId, b.CompanyId))
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var qty = g.Sum(x => x.Quantity);
                        var cost = g.Sum(x => x.Quantity * x.PricePerShare);
                        return qty > 0 ? cost / qty : 0m;
                    });

            recentTransactions = rawRecent.Select(t =>
            {
                var totalVal = Math.Round(t.Quantity * t.PricePerShare, 2);
                var feeVal = Math.Round(t.Fee, 2);
                var netVal = t.TransactionType == "BUY" ? totalVal + feeVal : totalVal - feeVal;
                decimal? rPL = null;
                if (t.TransactionType == "SELL")
                {
                    avgCostMap.TryGetValue((t.PortfolioId, t.CompanyId), out var avgCost);
                    rPL = Math.Round((totalVal - feeVal) - (t.Quantity * avgCost), 2);
                }

                return new DashboardRecentTransactionDto
                {
                    TransactionId = t.TransactionId,
                    PortfolioId = t.PortfolioId,
                    PortfolioName = t.Portfolio?.PortfolioName ?? string.Empty,
                    CompanyId = t.CompanyId,
                    CompanyName = t.Company?.CompanyName ?? string.Empty,
                    TickerSymbol = t.Company?.TickerSymbol ?? string.Empty,
                    TransactionType = t.TransactionType,
                    Quantity = t.Quantity,
                    PricePerShare = t.PricePerShare,
                    TotalValue = totalVal,
                    Fee = feeVal,
                    NetAmount = netVal,
                    RealizedProfitLoss = rPL,
                    TransactionDate = t.TransactionDate
                };
            }).ToList();
        }

        // 8. Re-use Performance Snapshots & Chart Datasets
        var perfRes = await _reportService.GetPerformanceReportAsync(userId, portfolioId, period, cancellationToken);
        var perfData = perfRes.Data;

        var performance = new DashboardPerformanceDto
        {
            StartingValue = perfData?.StartingValue ?? 0m,
            CurrentValue = perfData?.CurrentValue ?? 0m,
            NetChange = perfData?.NetChange ?? 0m,
            NetChangePercentage = perfData?.NetChangePercentage ?? 0m,
            Labels = perfData?.ChartLabels ?? new List<string>(),
            Values = perfData?.ChartValues ?? new List<decimal>()
        };

        // 9. Re-use Sector Allocation
        var sectorRes = await _reportService.GetCompanySectorReportAsync(userId, portfolioId, cancellationToken);
        var sectorData = sectorRes.Data;

        var sectorAllocation = (sectorData?.Sectors ?? new List<DTOs.Reports.SectorAllocationItemDto>())
            .Select(s => new DashboardSectorAllocationDto
            {
                SectorId = s.SectorId,
                SectorName = s.SectorName,
                HoldingsCount = s.HoldingsCount,
                TotalInvested = s.TotalInvested,
                MarketValue = s.CurrentMarketValue,
                AllocationPercentage = s.AllocationPercentage
            }).ToList();

        var dashboardResponse = new DashboardResponseDto
        {
            UserFullName = userName,
            Summary = summary,
            RecentTransactions = recentTransactions,
            TopHoldings = topHoldings,
            Performance = performance,
            SectorAllocation = sectorAllocation
        };

        return ApiResponse<DashboardResponseDto>.Ok(dashboardResponse);
    }
}
