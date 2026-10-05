using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Companies;
using ShareSync.Application.DTOs.Dividends;
using ShareSync.Application.DTOs.Reports;
using ShareSync.Application.Interfaces;

namespace ShareSync.Application.Services;

public class CompanyService : ICompanyService
{
    private readonly IApplicationDbContext _context;
    private readonly IReportService _reportService;
    private readonly IDividendService _dividendService;

    public CompanyService(IApplicationDbContext context)
        : this(context, new ReportService(context), new DividendService(context))
    {
    }

    public CompanyService(
        IApplicationDbContext context,
        IReportService reportService,
        IDividendService dividendService)
    {
        _context = context;
        _reportService = reportService ?? new ReportService(context);
        _dividendService = dividendService ?? new DividendService(context);
    }

    public async Task<ApiResponse<List<CompanyDto>>> GetAllCompaniesAsync(CancellationToken cancellationToken = default)
    {
        var companies = await _context.Companies
            .AsNoTracking()
            .Include(c => c.Sector)
            .OrderBy(c => c.TickerSymbol)
            .Select(c => new CompanyDto
            {
                CompanyId = c.CompanyId,
                CompanyName = c.CompanyName,
                TickerSymbol = c.TickerSymbol,
                SectorId = c.SectorId,
                SectorName = c.Sector != null ? c.Sector.SectorName : string.Empty,
                CurrentPrice = c.CurrentPrice,
                MarketCap = c.MarketCap
            })
            .ToListAsync(cancellationToken);

        return ApiResponse<List<CompanyDto>>.Ok(companies);
    }

    public async Task<ApiResponse<CompanyDto>> GetCompanyByIdAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var company = await _context.Companies
            .AsNoTracking()
            .Include(c => c.Sector)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId, cancellationToken);

        if (company == null)
        {
            throw new NotFoundException("Company", companyId);
        }

        var dto = new CompanyDto
        {
            CompanyId = company.CompanyId,
            CompanyName = company.CompanyName,
            TickerSymbol = company.TickerSymbol,
            SectorId = company.SectorId,
            SectorName = company.Sector != null ? company.Sector.SectorName : string.Empty,
            CurrentPrice = company.CurrentPrice,
            MarketCap = company.MarketCap
        };

        return ApiResponse<CompanyDto>.Ok(dto);
    }

    public async Task<ApiResponse<CompanyPriceHistoryResponseDto>> GetCompanyPriceHistoryAsync(
        int companyId,
        CompanyPriceHistoryFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == companyId, cancellationToken);

        if (company == null)
        {
            throw new NotFoundException("Company", companyId);
        }

        var query = _context.CompanyPriceHistories
            .AsNoTracking()
            .Where(h => h.CompanyId == companyId);

        var now = DateTime.UtcNow;
        var period = filter?.Period?.Trim().ToUpperInvariant() ?? "ALL";

        DateTime? computedStart = filter?.StartDate;
        if (!computedStart.HasValue && !string.IsNullOrWhiteSpace(period) && period != "ALL")
        {
            computedStart = period switch
            {
                "1D" => now.AddDays(-1),
                "1W" => now.AddDays(-7),
                "1M" => now.AddMonths(-1),
                "3M" => now.AddMonths(-3),
                "6M" => now.AddMonths(-6),
                "1Y" => now.AddYears(-1),
                _ => null
            };
        }

        if (computedStart.HasValue)
        {
            query = query.Where(h => h.RecordedAt >= computedStart.Value);
        }

        if (filter?.EndDate.HasValue == true)
        {
            query = query.Where(h => h.RecordedAt <= filter.EndDate.Value);
        }

        var limit = filter?.Limit ?? 200;
        if (limit <= 0) limit = 100;
        if (limit > 500) limit = 500;

        var historyList = await query
            .OrderBy(h => h.RecordedAt)
            .Take(limit)
            .Select(h => new CompanyPriceHistoryDto
            {
                PriceHistoryId = h.PriceHistoryId,
                CompanyId = h.CompanyId,
                Price = h.Price,
                OpenPrice = h.OpenPrice,
                HighPrice = h.HighPrice,
                LowPrice = h.LowPrice,
                Volume = h.Volume,
                RecordedAt = h.RecordedAt
            })
            .ToListAsync(cancellationToken);

        decimal? high = historyList.Count > 0 ? historyList.Max(h => h.HighPrice ?? h.Price) : null;
        decimal? low = historyList.Count > 0 ? historyList.Min(h => h.LowPrice ?? h.Price) : null;

        decimal? change = null;
        decimal? changePct = null;

        if (historyList.Count > 0)
        {
            var firstPrice = historyList.First().Price;
            var lastPrice = historyList.Last().Price;
            change = Math.Round(lastPrice - firstPrice, 2);
            if (firstPrice > 0)
            {
                changePct = Math.Round((change.Value / firstPrice) * 100, 2);
            }
        }

        var responseDto = new CompanyPriceHistoryResponseDto
        {
            CompanyId = company.CompanyId,
            CompanyName = company.CompanyName,
            TickerSymbol = company.TickerSymbol,
            CurrentPrice = company.CurrentPrice,
            Period = period,
            TotalPoints = historyList.Count,
            PeriodHigh = high,
            PeriodLow = low,
            PeriodChange = change,
            PeriodChangePercentage = changePct,
            History = historyList
        };

        return ApiResponse<CompanyPriceHistoryResponseDto>.Ok(responseDto);
    }

    public async Task<ApiResponse<CompanyDetailDto>> GetCompanyDetailAsync(
        int companyId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var company = await _context.Companies
            .AsNoTracking()
            .Include(c => c.Sector)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId, cancellationToken);

        if (company == null)
        {
            throw new NotFoundException("Company", companyId);
        }

        // 1. User Position (Reusing ReportService for 100% financial consistency)
        CompanyUserPositionDto? userPosition = null;
        var holdingsReport = await _reportService.GetHoldingsReportAsync(userId, null, cancellationToken);
        var userCompanyHoldings = holdingsReport.Data?.Holdings?
            .Where(h => h.CompanyId == companyId && h.CurrentQuantity > 0)
            .ToList() ?? new List<PortfolioHoldingsReportItemDto>();

        if (userCompanyHoldings.Any())
        {
            var totalShares = userCompanyHoldings.Sum(h => h.CurrentQuantity);
            var totalCost = userCompanyHoldings.Sum(h => h.InvestedValue);
            var avgBuy = totalShares > 0 ? Math.Round(totalCost / totalShares, 2) : 0m;
            var totalMarketVal = userCompanyHoldings.Sum(h => h.CurrentMarketValue);
            var totalPL = userCompanyHoldings.Sum(h => h.UnrealizedProfitLoss);
            var returnPct = totalCost > 0 ? Math.Round((totalPL / totalCost) * 100, 2) : 0m;

            userPosition = new CompanyUserPositionDto
            {
                Shares = totalShares,
                AverageBuyPrice = avgBuy,
                CostBasis = totalCost,
                CurrentMarketValue = totalMarketVal,
                UnrealizedProfitLoss = totalPL,
                ReturnPercentage = returnPct,
                PortfolioBreakdown = userCompanyHoldings.Select(h => new CompanyPortfolioBreakdownDto
                {
                    PortfolioId = h.PortfolioId,
                    PortfolioName = h.PortfolioName,
                    Shares = h.CurrentQuantity,
                    AverageBuyPrice = h.WeightedAverageBuyPrice,
                    MarketValue = h.CurrentMarketValue,
                    UnrealizedProfitLoss = h.UnrealizedProfitLoss,
                    ReturnPercentage = h.ProfitLossPercentage
                }).ToList()
            };
        }

        // 2. Watchlist Status (Strictly scoped to authenticated user)
        var watchlistItem = await _context.WatchlistItems
            .AsNoTracking()
            .Include(wi => wi.Watchlist)
            .Where(wi => wi.CompanyId == companyId && wi.Watchlist != null && wi.Watchlist.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        bool isInWatchlist = watchlistItem != null;
        int? watchlistId = watchlistItem?.WatchlistId;
        string? watchlistName = watchlistItem?.Watchlist?.WatchlistName;
        decimal? targetPrice = watchlistItem?.TargetPrice;
        decimal? targetDistancePct = null;
        bool isTargetReached = false;

        if (targetPrice.HasValue && targetPrice.Value > 0)
        {
            isTargetReached = company.CurrentPrice >= targetPrice.Value;
            if (company.CurrentPrice > 0)
            {
                targetDistancePct = Math.Round(Math.Abs(company.CurrentPrice - targetPrice.Value) / company.CurrentPrice * 100, 2);
            }
        }

        // 3. Dividend History (Reusing DividendService)
        var divFilter = new DividendFilterDto { CompanyId = companyId };
        var divResponse = await _dividendService.GetDividendsAsync(divFilter, userId, cancellationToken);
        var dividendItems = (divResponse.Data ?? new List<DividendDto>())
            .OrderByDescending(d => d.PaymentDate)
            .Select(d => new CompanyDividendItemDto
            {
                DividendId = d.DividendId,
                DividendPerShare = d.DividendPerShare,
                DeclarationDate = d.DeclarationDate,
                PaymentDate = d.PaymentDate,
                Status = d.Status,
                UserSharesHeld = d.UserSharesHeld,
                EstimatedIncome = d.EstimatedIncome
            })
            .ToList();

        // 4. Quick Performance Stats from Price History
        var oneYearAgo = DateTime.UtcNow.AddYears(-1);
        var priceHistory = await _context.CompanyPriceHistories
            .AsNoTracking()
            .Where(h => h.CompanyId == companyId && h.RecordedAt >= oneYearAgo)
            .OrderBy(h => h.RecordedAt)
            .ToListAsync(cancellationToken);

        decimal? yearHigh = priceHistory.Any() ? priceHistory.Max(h => h.HighPrice ?? h.Price) : company.CurrentPrice;
        decimal? yearLow = priceHistory.Any() ? priceHistory.Min(h => h.LowPrice ?? h.Price) : company.CurrentPrice;

        decimal? dayChange = null;
        decimal? dayChangePct = null;
        if (priceHistory.Count >= 2)
        {
            var latest = priceHistory[^1];
            var prev = priceHistory[^2];
            dayChange = Math.Round(latest.Price - prev.Price, 2);
            dayChangePct = prev.Price > 0 ? Math.Round((dayChange.Value / prev.Price) * 100, 2) : 0m;
        }

        var detailDto = new CompanyDetailDto
        {
            CompanyId = company.CompanyId,
            CompanyName = company.CompanyName,
            TickerSymbol = company.TickerSymbol,
            SectorId = company.SectorId,
            SectorName = company.Sector?.SectorName ?? string.Empty,
            CurrentPrice = company.CurrentPrice,
            MarketCap = company.MarketCap,
            YearHigh = yearHigh,
            YearLow = yearLow,
            DayChange = dayChange,
            DayChangePercentage = dayChangePct,
            UserHolding = userPosition,
            IsInWatchlist = isInWatchlist,
            WatchlistId = watchlistId,
            WatchlistName = watchlistName,
            TargetPrice = targetPrice,
            TargetDistancePercentage = targetDistancePct,
            IsTargetReached = isTargetReached,
            Dividends = dividendItems
        };

        return ApiResponse<CompanyDetailDto>.Ok(detailDto);
    }
}
