using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Reports;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Services;

public class ReportService : IReportService
{
    private readonly IApplicationDbContext _context;

    public ReportService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<PortfolioHoldingsReportDto>> GetHoldingsReportAsync(
        int userId,
        int? portfolioId = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify portfolio ownership if specified
        if (portfolioId.HasValue)
        {
            var p = await _context.Portfolios
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PortfolioId == portfolioId.Value, cancellationToken);

            if (p == null)
            {
                throw new NotFoundException("Portfolio", portfolioId.Value);
            }

            if (p.UserId != userId)
            {
                throw new AppException("You do not have permission to access this portfolio.", 403);
            }
        }

        // 2. Get user's valid portfolio IDs
        var userPortfolioIds = await _context.Portfolios
            .AsNoTracking()
            .Where(p => p.UserId == userId && (!portfolioId.HasValue || p.PortfolioId == portfolioId.Value))
            .Select(p => p.PortfolioId)
            .ToListAsync(cancellationToken);

        if (!userPortfolioIds.Any())
        {
            return ApiResponse<PortfolioHoldingsReportDto>.Ok(new PortfolioHoldingsReportDto());
        }

        // 3. Query the Oracle view VW_PORTFOLIO_HOLDINGS
        var viewHoldings = await _context.PortfolioHoldings
            .AsNoTracking()
            .Where(h => userPortfolioIds.Contains(h.PortfolioId))
            .OrderByDescending(h => h.CurrentMarketValue)
            .ToListAsync(cancellationToken);

        List<PortfolioHoldingsReportItemDto> items;

        if (viewHoldings.Any())
        {
            items = viewHoldings.Select(h =>
            {
                var invested = Math.Round(h.CurrentQuantity * h.WeightedAverageBuyPrice, 2);
                var plPct = invested > 0
                    ? Math.Round((h.UnrealizedProfitLoss / invested) * 100, 2)
                    : 0m;

                return new PortfolioHoldingsReportItemDto
                {
                    CompanyId = h.CompanyId,
                    CompanyName = h.CompanyName,
                    TickerSymbol = h.TickerSymbol,
                    PortfolioId = h.PortfolioId,
                    PortfolioName = h.PortfolioName,
                    CurrentQuantity = h.CurrentQuantity,
                    WeightedAverageBuyPrice = Math.Round(h.WeightedAverageBuyPrice, 2),
                    CurrentMarketPrice = Math.Round(h.CurrentPrice, 2),
                    InvestedValue = invested,
                    CurrentMarketValue = Math.Round(h.CurrentMarketValue, 2),
                    UnrealizedProfitLoss = Math.Round(h.UnrealizedProfitLoss, 2),
                    ProfitLossPercentage = plPct
                };
            }).ToList();
        }
        else
        {
            // Fallback for InMemory tests or when view is empty: calculate directly from transactions
            var txs = await _context.Transactions
                .AsNoTracking()
                .Include(t => t.Company)
                .Include(t => t.Portfolio)
                .Where(t => userPortfolioIds.Contains(t.PortfolioId) && t.Company != null)
                .ToListAsync(cancellationToken);

            items = new List<PortfolioHoldingsReportItemDto>();
            var grouped = txs.GroupBy(t => new { t.PortfolioId, t.CompanyId });

            foreach (var g in grouped)
            {
                var company = g.First().Company!;
                var portfolio = g.First().Portfolio!;
                var buyTxs = g.Where(t => t.TransactionType == "BUY").ToList();
                var sellTxs = g.Where(t => t.TransactionType == "SELL").ToList();

                var buyQty = buyTxs.Sum(t => t.Quantity);
                var sellQty = sellTxs.Sum(t => t.Quantity);
                var netQty = buyQty - sellQty;

                if (netQty <= 0) continue;

                var totalBuyCost = buyTxs.Sum(t => t.Quantity * t.PricePerShare);
                var avgBuyPrice = buyQty > 0 ? totalBuyCost / buyQty : 0m;
                var currentPrice = company.CurrentPrice;
                var marketVal = netQty * currentPrice;
                var investedVal = netQty * avgBuyPrice;
                var unrealizedPL = marketVal - investedVal;
                var plPct = investedVal > 0 ? (unrealizedPL / investedVal) * 100 : 0m;

                items.Add(new PortfolioHoldingsReportItemDto
                {
                    CompanyId = company.CompanyId,
                    CompanyName = company.CompanyName,
                    TickerSymbol = company.TickerSymbol,
                    PortfolioId = portfolio.PortfolioId,
                    PortfolioName = portfolio.PortfolioName,
                    CurrentQuantity = netQty,
                    WeightedAverageBuyPrice = Math.Round(avgBuyPrice, 2),
                    CurrentMarketPrice = Math.Round(currentPrice, 2),
                    InvestedValue = Math.Round(investedVal, 2),
                    CurrentMarketValue = Math.Round(marketVal, 2),
                    UnrealizedProfitLoss = Math.Round(unrealizedPL, 2),
                    ProfitLossPercentage = Math.Round(plPct, 2)
                });
            }

            items = items.OrderByDescending(i => i.CurrentMarketValue).ToList();
        }

        var totalInvested = Math.Round(items.Sum(i => i.InvestedValue), 2);
        var totalMarketValue = Math.Round(items.Sum(i => i.CurrentMarketValue), 2);
        var totalUnrealizedPL = Math.Round(items.Sum(i => i.UnrealizedProfitLoss), 2);
        var totalPLPct = totalInvested > 0
            ? Math.Round((totalUnrealizedPL / totalInvested) * 100, 2)
            : 0m;

        var report = new PortfolioHoldingsReportDto
        {
            TotalInvested = totalInvested,
            TotalMarketValue = totalMarketValue,
            TotalUnrealizedProfitLoss = totalUnrealizedPL,
            TotalProfitLossPercentage = totalPLPct,
            HoldingsCount = items.Count,
            Holdings = items
        };

        return ApiResponse<PortfolioHoldingsReportDto>.Ok(report);
    }

    public async Task<ApiResponse<PortfolioPerformanceReportDto>> GetPerformanceReportAsync(
        int userId,
        int? portfolioId = null,
        string? period = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Resolve portfolio
        var userPortfolios = await _context.Portfolios
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        if (!userPortfolios.Any())
        {
            return ApiResponse<PortfolioPerformanceReportDto>.Ok(new PortfolioPerformanceReportDto());
        }

        Portfolio targetPortfolio;
        if (portfolioId.HasValue)
        {
            targetPortfolio = userPortfolios.FirstOrDefault(p => p.PortfolioId == portfolioId.Value)
                ?? throw new AppException("Portfolio not found or access denied.", 404);
        }
        else
        {
            var portfolioIds = userPortfolios.Select(p => p.PortfolioId).ToList();
            var portfolioWithSnapshots = await _context.PortfolioSnapshots
                .AsNoTracking()
                .Where(s => portfolioIds.Contains(s.PortfolioId))
                .OrderByDescending(s => s.SnapshotDate)
                .Select(s => (int?)s.PortfolioId)
                .FirstOrDefaultAsync(cancellationToken);

            targetPortfolio = (portfolioWithSnapshots.HasValue
                ? userPortfolios.FirstOrDefault(p => p.PortfolioId == portfolioWithSnapshots.Value)
                : null)
                ?? userPortfolios.OrderBy(p => p.PortfolioId).First();
        }

        // 2. Parse period filter
        var now = DateTime.UtcNow.Date;
        DateTime? startDate = null;

        if (!string.IsNullOrWhiteSpace(period))
        {
            var pLower = period.Trim().ToLowerInvariant();
            if (pLower == "6" || pLower.Contains("6 month") || pLower == "6m")
            {
                startDate = now.AddMonths(-6);
            }
            else if (pLower == "12" || pLower.Contains("12 month") || pLower.Contains("1 year") || pLower == "1y")
            {
                startDate = now.AddMonths(-12);
            }
            else if (pLower == "ytd")
            {
                startDate = new DateTime(now.Year, 1, 1);
            }
            else if (pLower.Contains("1 month") || pLower == "1m")
            {
                startDate = now.AddMonths(-1);
            }
        }

        // 3. Query snapshots
        var query = _context.PortfolioSnapshots
            .AsNoTracking()
            .Where(s => s.PortfolioId == targetPortfolio.PortfolioId);

        if (startDate.HasValue)
        {
            query = query.Where(s => s.SnapshotDate >= startDate.Value);
        }

        var snapshots = await query
            .OrderBy(s => s.SnapshotDate)
            .ToListAsync(cancellationToken);

        var report = new PortfolioPerformanceReportDto
        {
            PortfolioId = targetPortfolio.PortfolioId,
            PortfolioName = targetPortfolio.PortfolioName
        };

        if (snapshots.Any())
        {
            decimal? prevValue = null;

            foreach (var s in snapshots)
            {
                decimal? change = null;
                decimal? pct = null;

                if (prevValue.HasValue)
                {
                    change = s.TotalValue - prevValue.Value;
                    pct = prevValue.Value > 0
                        ? Math.Round((change.Value / prevValue.Value) * 100, 2)
                        : 0m;
                }

                report.ChartLabels.Add(s.SnapshotDate.ToString("MMM dd"));
                report.ChartValues.Add(s.TotalValue);

                report.Snapshots.Add(new PerformanceSnapshotItemDto
                {
                    SnapshotId = s.SnapshotId,
                    SnapshotDate = s.SnapshotDate,
                    PortfolioValue = s.TotalValue,
                    ChangeFromPrevious = change,
                    PercentageChange = pct
                });

                prevValue = s.TotalValue;
            }

            report.StartingValue = snapshots.First().TotalValue;
            report.CurrentValue = snapshots.Last().TotalValue;
            report.NetChange = report.CurrentValue - report.StartingValue;
            report.NetChangePercentage = report.StartingValue > 0
                ? Math.Round((report.NetChange / report.StartingValue) * 100, 2)
                : 0m;
        }
        else
        {
            // If no historical snapshots, calculate current holdings value
            var currentVal = await CalculatePortfolioHoldingsValueAsync(targetPortfolio.PortfolioId, cancellationToken);
            report.ChartLabels.Add(now.ToString("MMM dd"));
            report.ChartValues.Add(currentVal);
            report.StartingValue = currentVal;
            report.CurrentValue = currentVal;
            report.NetChange = 0;
            report.NetChangePercentage = 0;
        }

        return ApiResponse<PortfolioPerformanceReportDto>.Ok(report);
    }

    public async Task<ApiResponse<TransactionHistoryReportDto>> GetTransactionHistoryReportAsync(
        int userId,
        int? portfolioId = null,
        int? companyId = null,
        string? transactionType = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Join TRANSACTIONS with PORTFOLIOS and COMPANIES with user isolation
        var query = _context.Transactions
            .AsNoTracking()
            .Include(t => t.Portfolio)
            .Include(t => t.Company)
            .Where(t => t.Portfolio.UserId == userId);

        if (portfolioId.HasValue)
        {
            query = query.Where(t => t.PortfolioId == portfolioId.Value);
        }

        if (companyId.HasValue)
        {
            query = query.Where(t => t.CompanyId == companyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(transactionType))
        {
            var typeUpper = transactionType.Trim().ToUpperInvariant();
            query = query.Where(t => t.TransactionType == typeUpper);
        }

        if (startDate.HasValue)
        {
            var start = startDate.Value.Date;
            query = query.Where(t => t.TransactionDate >= start);
        }

        if (endDate.HasValue)
        {
            var end = endDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(t => t.TransactionDate <= end);
        }

        var transactions = await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.TransactionId)
            .ToListAsync(cancellationToken);

        var items = transactions.Select(t => new TransactionHistoryReportItemDto
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
            TotalTransactionValue = Math.Round(t.Quantity * t.PricePerShare, 2),
            TransactionDate = t.TransactionDate
        }).ToList();

        var buyItems = items.Where(i => i.TransactionType == "BUY").ToList();
        var sellItems = items.Where(i => i.TransactionType == "SELL").ToList();

        var totalBuyVal = Math.Round(buyItems.Sum(i => i.TotalTransactionValue), 2);
        var totalSellVal = Math.Round(sellItems.Sum(i => i.TotalTransactionValue), 2);

        var report = new TransactionHistoryReportDto
        {
            TotalTransactions = items.Count,
            BuyCount = buyItems.Count,
            SellCount = sellItems.Count,
            TotalBuyValue = totalBuyVal,
            TotalSellValue = totalSellVal,
            NetCashFlow = totalSellVal - totalBuyVal,
            Transactions = items
        };

        return ApiResponse<TransactionHistoryReportDto>.Ok(report);
    }

    public async Task<ApiResponse<CompanySectorReportDto>> GetCompanySectorReportAsync(
        int userId,
        int? portfolioId = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify portfolio if specified
        if (portfolioId.HasValue)
        {
            var p = await _context.Portfolios
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PortfolioId == portfolioId.Value, cancellationToken);

            if (p == null)
            {
                throw new NotFoundException("Portfolio", portfolioId.Value);
            }

            if (p.UserId != userId)
            {
                throw new AppException("You do not have permission to access this portfolio.", 403);
            }
        }

        // 2. Query user's transactions grouped by company with sector details
        var userPortfolioIds = await _context.Portfolios
            .AsNoTracking()
            .Where(p => p.UserId == userId && (!portfolioId.HasValue || p.PortfolioId == portfolioId.Value))
            .Select(p => p.PortfolioId)
            .ToListAsync(cancellationToken);

        if (!userPortfolioIds.Any())
        {
            return ApiResponse<CompanySectorReportDto>.Ok(new CompanySectorReportDto());
        }

        var transactions = await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Company)
                .ThenInclude(c => c.Sector)
            .Where(t => userPortfolioIds.Contains(t.PortfolioId) && t.Company != null)
            .ToListAsync(cancellationToken);

        var companyList = new List<CompanyInvestmentItemDto>();
        var companyGroups = transactions.GroupBy(t => t.CompanyId);

        foreach (var cg in companyGroups)
        {
            var comp = cg.First().Company!;
            var buyQty = cg.Where(t => t.TransactionType == "BUY").Sum(t => t.Quantity);
            var sellQty = cg.Where(t => t.TransactionType == "SELL").Sum(t => t.Quantity);
            var netQty = buyQty - sellQty;

            if (netQty <= 0) continue;

            var totalBuyCost = cg.Where(t => t.TransactionType == "BUY").Sum(t => t.Quantity * t.PricePerShare);
            var avgBuyPrice = buyQty > 0 ? totalBuyCost / buyQty : 0m;
            var invested = Math.Round(netQty * avgBuyPrice, 2);
            var marketVal = Math.Round(netQty * comp.CurrentPrice, 2);
            var pl = marketVal - invested;

            companyList.Add(new CompanyInvestmentItemDto
            {
                CompanyId = comp.CompanyId,
                CompanyName = comp.CompanyName,
                TickerSymbol = comp.TickerSymbol,
                SectorName = comp.Sector?.SectorName ?? "Unassigned",
                SharesHeld = netQty,
                TotalInvested = invested,
                CurrentMarketValue = marketVal,
                UnrealizedProfitLoss = pl
            });
        }

        var totalPortfolioValue = Math.Round(companyList.Sum(c => c.CurrentMarketValue), 2);

        // Calculate company allocation percentages
        foreach (var c in companyList)
        {
            c.AllocationPercentage = totalPortfolioValue > 0
                ? Math.Round((c.CurrentMarketValue / totalPortfolioValue) * 100, 2)
                : 0m;
        }

        // Aggregate by sector using GROUP BY logic
        var sectors = companyList
            .GroupBy(c => c.SectorName)
            .Select(sg =>
            {
                var sMarketVal = Math.Round(sg.Sum(c => c.CurrentMarketValue), 2);
                var sInvested = Math.Round(sg.Sum(c => c.TotalInvested), 2);
                var sAlloc = totalPortfolioValue > 0
                    ? Math.Round((sMarketVal / totalPortfolioValue) * 100, 2)
                    : 0m;

                return new SectorAllocationItemDto
                {
                    SectorId = 0,
                    SectorName = sg.Key,
                    HoldingsCount = sg.Count(),
                    TotalInvested = sInvested,
                    CurrentMarketValue = sMarketVal,
                    UnrealizedProfitLoss = sMarketVal - sInvested,
                    AllocationPercentage = sAlloc
                };
            })
            .OrderByDescending(s => s.CurrentMarketValue)
            .ToList();

        var report = new CompanySectorReportDto
        {
            TotalPortfolioValue = totalPortfolioValue,
            Sectors = sectors,
            Companies = companyList.OrderByDescending(c => c.CurrentMarketValue).ToList()
        };

        return ApiResponse<CompanySectorReportDto>.Ok(report);
    }

    public async Task<ApiResponse<DividendIncomeReportDto>> GetDividendIncomeReportAsync(
        int userId,
        int? companyId = null,
        int? year = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Calculate user's shares held per company across all user portfolios
        var userPortfolioIds = await _context.Portfolios
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.PortfolioId)
            .ToListAsync(cancellationToken);

        var transactions = await _context.Transactions
            .AsNoTracking()
            .Where(t => userPortfolioIds.Contains(t.PortfolioId))
            .ToListAsync(cancellationToken);

        var userSharesMap = transactions
            .GroupBy(t => t.CompanyId)
            .ToDictionary(
                g => g.Key,
                g => g.Where(t => t.TransactionType == "BUY").Sum(t => t.Quantity) -
                     g.Where(t => t.TransactionType == "SELL").Sum(t => t.Quantity)
            );

        // 2. Query dividends joined with companies
        var query = _context.Dividends
            .AsNoTracking()
            .Include(d => d.Company)
            .AsQueryable();

        if (companyId.HasValue)
        {
            query = query.Where(d => d.CompanyId == companyId.Value);
        }

        if (year.HasValue)
        {
            query = query.Where(d => d.PaymentDate.Year == year.Value);
        }

        if (startDate.HasValue)
        {
            var start = startDate.Value.Date;
            query = query.Where(d => d.PaymentDate >= start);
        }

        if (endDate.HasValue)
        {
            var end = endDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(d => d.PaymentDate <= end);
        }

        var dividends = await query
            .OrderByDescending(d => d.PaymentDate)
            .ThenByDescending(d => d.DividendId)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow.Date;
        var items = new List<DividendReportItemDto>();

        foreach (var d in dividends)
        {
            userSharesMap.TryGetValue(d.CompanyId, out var held);
            if (held < 0) held = 0;

            var estimated = Math.Round(held * d.DividendPerShare, 2);
            var isPaid = d.PaymentDate.Date <= now;

            items.Add(new DividendReportItemDto
            {
                DividendId = d.DividendId,
                CompanyId = d.CompanyId,
                CompanyName = d.Company?.CompanyName ?? string.Empty,
                TickerSymbol = d.Company?.TickerSymbol ?? string.Empty,
                DividendPerShare = d.DividendPerShare,
                DeclarationDate = d.DeclarationDate,
                PaymentDate = d.PaymentDate,
                UserSharesHeld = held,
                EstimatedIncome = estimated,
                Status = isPaid ? "Paid" : "Upcoming"
            });
        }

        // Group by Company
        var companyBreakdown = items
            .GroupBy(i => i.CompanyId)
            .Select(cg => new CompanyDividendAggregationDto
            {
                CompanyId = cg.Key,
                CompanyName = cg.First().CompanyName,
                TickerSymbol = cg.First().TickerSymbol,
                TotalDividendPerShare = cg.Sum(x => x.DividendPerShare),
                TotalIncome = cg.Sum(x => x.EstimatedIncome),
                PaymentsCount = cg.Count()
            })
            .OrderByDescending(cg => cg.TotalIncome)
            .ToList();

        // Group by Period (Year)
        var periodBreakdown = items
            .GroupBy(i => i.PaymentDate.Year)
            .Select(yg => new PeriodDividendAggregationDto
            {
                Year = yg.Key,
                TotalIncome = yg.Sum(x => x.EstimatedIncome),
                PaymentsCount = yg.Count()
            })
            .OrderByDescending(yg => yg.Year)
            .ToList();

        var totalIncome = Math.Round(items.Sum(i => i.EstimatedIncome), 2);
        var thisYearIncome = Math.Round(items.Where(i => i.PaymentDate.Year == now.Year).Sum(i => i.EstimatedIncome), 2);
        var upcomingIncome = Math.Round(items.Where(i => i.Status == "Upcoming").Sum(i => i.EstimatedIncome), 2);

        var report = new DividendIncomeReportDto
        {
            TotalIncome = totalIncome,
            ThisYearIncome = thisYearIncome,
            UpcomingIncome = upcomingIncome,
            TotalPaymentsCount = items.Count,
            CompanyBreakdown = companyBreakdown,
            PeriodBreakdown = periodBreakdown,
            Dividends = items
        };

        return ApiResponse<DividendIncomeReportDto>.Ok(report);
    }

    public async Task<ApiResponse<WatchlistTargetReportDto>> GetWatchlistTargetReportAsync(
        int userId,
        int? watchlistId = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Query user watchlists and items
        var query = _context.Watchlists
            .AsNoTracking()
            .Include(w => w.Items)
                .ThenInclude(wi => wi.Company)
            .Where(w => w.UserId == userId);

        if (watchlistId.HasValue)
        {
            query = query.Where(w => w.WatchlistId == watchlistId.Value);
        }

        var watchlists = await query.ToListAsync(cancellationToken);

        var items = new List<WatchlistTargetItemDto>();

        foreach (var w in watchlists)
        {
            foreach (var wi in w.Items)
            {
                if (wi.Company == null) continue;

                var target = wi.TargetPrice ?? wi.Company.CurrentPrice;
                var diff = Math.Round(target - wi.Company.CurrentPrice, 2);
                var pctDiff = wi.Company.CurrentPrice > 0
                    ? Math.Round((diff / wi.Company.CurrentPrice) * 100, 2)
                    : 0m;

                string status;
                if (wi.Company.CurrentPrice >= target)
                {
                    status = "Target Reached";
                }
                else if (wi.Company.CurrentPrice >= target * 0.95m)
                {
                    status = "Near Target";
                }
                else
                {
                    status = "Below Target";
                }

                items.Add(new WatchlistTargetItemDto
                {
                    WatchlistId = w.WatchlistId,
                    WatchlistName = w.WatchlistName,
                    CompanyId = wi.CompanyId,
                    CompanyName = wi.Company.CompanyName,
                    TickerSymbol = wi.Company.TickerSymbol,
                    CurrentPrice = wi.Company.CurrentPrice,
                    TargetPrice = target,
                    PriceDifference = diff,
                    PercentageDifference = pctDiff,
                    Status = status
                });
            }
        }

        var report = new WatchlistTargetReportDto
        {
            TotalItems = items.Count,
            AboveTargetCount = items.Count(i => i.Status == "Target Reached"),
            NearTargetCount = items.Count(i => i.Status == "Near Target"),
            BelowTargetCount = items.Count(i => i.Status == "Below Target"),
            Items = items.OrderBy(i => i.PriceDifference).ToList()
        };

        return ApiResponse<WatchlistTargetReportDto>.Ok(report);
    }

    public async Task<ApiResponse<ReportSummaryDto>> GetReportSummaryAsync(
        int userId,
        int? portfolioId = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Holdings value & unrealized P/L
        var holdingsRes = await GetHoldingsReportAsync(userId, portfolioId, cancellationToken);
        var holdings = holdingsRes.Data;

        // 2. Dividend income
        var divRes = await GetDividendIncomeReportAsync(userId, null, null, null, null, cancellationToken);
        var dividends = divRes.Data;

        // 3. Transactions count
        var userPortfolioIds = await _context.Portfolios
            .AsNoTracking()
            .Where(p => p.UserId == userId && (!portfolioId.HasValue || p.PortfolioId == portfolioId.Value))
            .Select(p => p.PortfolioId)
            .ToListAsync(cancellationToken);

        var txCount = await _context.Transactions
            .AsNoTracking()
            .CountAsync(t => userPortfolioIds.Contains(t.PortfolioId), cancellationToken);

        var summary = new ReportSummaryDto
        {
            TotalPortfolioValue = holdings?.TotalMarketValue ?? 0m,
            TotalUnrealizedProfitLoss = holdings?.TotalUnrealizedProfitLoss ?? 0m,
            TotalDividendIncome = dividends?.TotalIncome ?? 0m,
            TotalTransactionsCount = txCount
        };

        return ApiResponse<ReportSummaryDto>.Ok(summary);
    }

    private async Task<decimal> CalculatePortfolioHoldingsValueAsync(int portfolioId, CancellationToken cancellationToken)
    {
        var transactions = await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Company)
            .Where(t => t.PortfolioId == portfolioId && t.Company != null)
            .ToListAsync(cancellationToken);

        if (!transactions.Any()) return 0m;

        decimal total = 0m;
        var groups = transactions.GroupBy(t => t.CompanyId);

        foreach (var group in groups)
        {
            var buyQty = group.Where(t => t.TransactionType == "BUY").Sum(t => t.Quantity);
            var sellQty = group.Where(t => t.TransactionType == "SELL").Sum(t => t.Quantity);
            var netQty = buyQty - sellQty;

            if (netQty > 0)
            {
                var price = group.First().Company!.CurrentPrice;
                total += netQty * price;
            }
        }

        return Math.Round(total, 2);
    }
}
