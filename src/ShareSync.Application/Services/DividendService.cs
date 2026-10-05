using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Dividends;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Services;

public class DividendService : IDividendService
{
    private readonly IApplicationDbContext _context;

    public DividendService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<DividendDto>>> GetDividendsAsync(DividendFilterDto filter, int userId, CancellationToken cancellationToken = default)
    {
        var query = _context.Dividends
            .AsNoTracking()
            .Include(d => d.Company)
            .AsQueryable();

        if (filter.CompanyId.HasValue && filter.CompanyId.Value > 0)
        {
            query = query.Where(d => d.CompanyId == filter.CompanyId.Value);
        }

        if (filter.StartDate.HasValue)
        {
            query = query.Where(d => d.PaymentDate >= filter.StartDate.Value);
        }

        if (filter.EndDate.HasValue)
        {
            query = query.Where(d => d.PaymentDate <= filter.EndDate.Value);
        }

        if (filter.Year.HasValue && filter.Year.Value > 0)
        {
            query = query.Where(d => d.PaymentDate.Year == filter.Year.Value);
        }

        var dividends = await query
            .OrderByDescending(d => d.PaymentDate)
            .ThenByDescending(d => d.DividendId)
            .ToListAsync(cancellationToken);

        var userSharesMap = await GetUserSharesMapAsync(userId, cancellationToken);
        var today = DateTime.UtcNow.Date;

        var dtos = dividends.Select(d => MapToDto(d, userSharesMap, today)).ToList();

        return ApiResponse<List<DividendDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<DividendDto>> GetDividendByIdAsync(int dividendId, int userId, CancellationToken cancellationToken = default)
    {
        var dividend = await _context.Dividends
            .AsNoTracking()
            .Include(d => d.Company)
            .FirstOrDefaultAsync(d => d.DividendId == dividendId, cancellationToken);

        if (dividend == null)
        {
            throw new NotFoundException("Dividend", dividendId);
        }

        var userSharesMap = await GetUserSharesMapAsync(userId, cancellationToken);
        var dto = MapToDto(dividend, userSharesMap, DateTime.UtcNow.Date);

        return ApiResponse<DividendDto>.Ok(dto);
    }

    public async Task<ApiResponse<DividendSummaryDto>> GetDividendSummaryAsync(int userId, CancellationToken cancellationToken = default)
    {
        var dividends = await _context.Dividends
            .AsNoTracking()
            .Include(d => d.Company)
            .ToListAsync(cancellationToken);

        var userSharesMap = await GetUserSharesMapAsync(userId, cancellationToken);
        var today = DateTime.UtcNow.Date;
        var currentYear = today.Year;

        decimal totalIncome = 0;
        decimal thisYearIncome = 0;
        decimal upcomingIncome = 0;

        foreach (var d in dividends)
        {
            var shares = userSharesMap.TryGetValue(d.CompanyId, out var s) ? Math.Max(0, s) : 0;
            var income = Math.Round(shares * d.DividendPerShare, 2);

            totalIncome += income;

            if (d.PaymentDate.Year == currentYear)
            {
                thisYearIncome += income;
            }

            if (d.PaymentDate > today)
            {
                upcomingIncome += income;
            }
        }

        // Company summaries
        var companySummaries = dividends
            .Where(d => d.Company != null)
            .GroupBy(d => d.CompanyId)
            .Select(g =>
            {
                var comp = g.First().Company!;
                var shares = userSharesMap.TryGetValue(comp.CompanyId, out var s) ? Math.Max(0, s) : 0;
                var totalDivPerShare = g.Sum(d => d.DividendPerShare);
                var totalEstIncome = Math.Round(shares * totalDivPerShare, 2);

                return new CompanyDividendSummaryDto
                {
                    CompanyId = comp.CompanyId,
                    TickerSymbol = comp.TickerSymbol,
                    CompanyName = comp.CompanyName,
                    TotalDividendPerShare = totalDivPerShare,
                    UserSharesHeld = shares,
                    TotalEstimatedIncome = totalEstIncome,
                    DividendsCount = g.Count()
                };
            })
            .OrderByDescending(c => c.TotalEstimatedIncome)
            .ThenBy(c => c.TickerSymbol)
            .ToList();

        var summary = new DividendSummaryDto
        {
            TotalIncome = Math.Round(totalIncome, 2),
            ThisYearIncome = Math.Round(thisYearIncome, 2),
            UpcomingIncome = Math.Round(upcomingIncome, 2),
            CompaniesCount = dividends.Select(d => d.CompanyId).Distinct().Count(),
            CompanySummaries = companySummaries
        };

        return ApiResponse<DividendSummaryDto>.Ok(summary);
    }

    public async Task<ApiResponse<DividendDto>> CreateDividendAsync(CreateDividendRequestDto request, int userId, CancellationToken cancellationToken = default)
    {
        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.CompanyId == request.CompanyId, cancellationToken);

        if (company == null)
        {
            throw new NotFoundException("Company", request.CompanyId);
        }

        if (request.DividendPerShare <= 0)
        {
            throw new AppException("Dividend per share must be greater than zero.", 400);
        }

        if (request.PaymentDate.Date < request.DeclarationDate.Date)
        {
            throw new AppException("Payment date must not be earlier than declaration date.", 400);
        }

        var dividend = new Dividend
        {
            CompanyId = request.CompanyId,
            DividendPerShare = request.DividendPerShare,
            DeclarationDate = request.DeclarationDate.Date,
            PaymentDate = request.PaymentDate.Date,
            Company = company
        };

        _context.Dividends.Add(dividend);
        await _context.SaveChangesAsync(cancellationToken);

        var userSharesMap = await GetUserSharesMapAsync(userId, cancellationToken);
        var dto = MapToDto(dividend, userSharesMap, DateTime.UtcNow.Date);

        return ApiResponse<DividendDto>.Ok(dto, "Dividend record created successfully.");
    }

    public async Task<ApiResponse<DividendDto>> UpdateDividendAsync(int dividendId, UpdateDividendRequestDto request, int userId, CancellationToken cancellationToken = default)
    {
        var dividend = await _context.Dividends
            .Include(d => d.Company)
            .FirstOrDefaultAsync(d => d.DividendId == dividendId, cancellationToken);

        if (dividend == null)
        {
            throw new NotFoundException("Dividend", dividendId);
        }

        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.CompanyId == request.CompanyId, cancellationToken);

        if (company == null)
        {
            throw new NotFoundException("Company", request.CompanyId);
        }

        if (request.DividendPerShare <= 0)
        {
            throw new AppException("Dividend per share must be greater than zero.", 400);
        }

        if (request.PaymentDate.Date < request.DeclarationDate.Date)
        {
            throw new AppException("Payment date must not be earlier than declaration date.", 400);
        }

        dividend.CompanyId = request.CompanyId;
        dividend.DividendPerShare = request.DividendPerShare;
        dividend.DeclarationDate = request.DeclarationDate.Date;
        dividend.PaymentDate = request.PaymentDate.Date;
        dividend.Company = company;

        await _context.SaveChangesAsync(cancellationToken);

        var userSharesMap = await GetUserSharesMapAsync(userId, cancellationToken);
        var dto = MapToDto(dividend, userSharesMap, DateTime.UtcNow.Date);

        return ApiResponse<DividendDto>.Ok(dto, "Dividend record updated successfully.");
    }

    public async Task<ApiResponse> DeleteDividendAsync(int dividendId, int userId, CancellationToken cancellationToken = default)
    {
        var dividend = await _context.Dividends
            .FirstOrDefaultAsync(d => d.DividendId == dividendId, cancellationToken);

        if (dividend == null)
        {
            throw new NotFoundException("Dividend", dividendId);
        }

        _context.Dividends.Remove(dividend);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse.Ok("Dividend record deleted successfully.");
    }

    private async Task<Dictionary<int, decimal>> GetUserSharesMapAsync(int userId, CancellationToken cancellationToken)
    {
        var transactions = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.Portfolio != null && t.Portfolio.UserId == userId)
            .ToListAsync(cancellationToken);

        return transactions
            .GroupBy(t => t.CompanyId)
            .ToDictionary(
                g => g.Key,
                g => g.Where(t => t.TransactionType == "BUY").Sum(t => t.Quantity) -
                     g.Where(t => t.TransactionType == "SELL").Sum(t => t.Quantity)
            );
    }

    private static DividendDto MapToDto(Dividend d, Dictionary<int, decimal> userSharesMap, DateTime today)
    {
        var shares = userSharesMap.TryGetValue(d.CompanyId, out var s) ? Math.Max(0, s) : 0;
        var isPaid = d.PaymentDate.Date <= today;
        var status = isPaid ? "Paid" : "Upcoming";
        var estimatedIncome = Math.Round(shares * d.DividendPerShare, 2);

        return new DividendDto
        {
            DividendId = d.DividendId,
            CompanyId = d.CompanyId,
            CompanyName = d.Company?.CompanyName ?? string.Empty,
            TickerSymbol = d.Company?.TickerSymbol ?? string.Empty,
            DividendPerShare = d.DividendPerShare,
            DeclarationDate = d.DeclarationDate,
            PaymentDate = d.PaymentDate,
            Status = status,
            UserSharesHeld = shares,
            EstimatedIncome = estimatedIncome
        };
    }

    public async Task<ApiResponse<DividendAnalyticsDto>> GetDividendAnalyticsAsync(
        DividendAnalyticsFilterDto filter,
        int userId,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify User Portfolios and Access Control
        var userPortfolios = await _context.Portfolios
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        if (!userPortfolios.Any())
        {
            return ApiResponse<DividendAnalyticsDto>.Ok(new DividendAnalyticsDto
            {
                PortfolioName = "No Portfolios",
                MonthlyIncome = GenerateEmptyMonthlyBuckets(filter.Year ?? DateTime.UtcNow.Year)
            });
        }

        string contextName = "All Portfolios";
        List<int> targetPortfolioIds;

        if (filter.PortfolioId.HasValue && filter.PortfolioId.Value > 0)
        {
            var target = userPortfolios.FirstOrDefault(p => p.PortfolioId == filter.PortfolioId.Value);
            if (target == null)
            {
                var existsInDb = await _context.Portfolios
                    .AsNoTracking()
                    .AnyAsync(p => p.PortfolioId == filter.PortfolioId.Value, cancellationToken);

                if (!existsInDb)
                {
                    throw new NotFoundException("Portfolio", filter.PortfolioId.Value);
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

        // 2. Compute user's shares held per company scoped to target portfolio(s)
        var transactions = await _context.Transactions
            .AsNoTracking()
            .Where(t => targetPortfolioIds.Contains(t.PortfolioId))
            .ToListAsync(cancellationToken);

        var userSharesMap = transactions
            .GroupBy(t => t.CompanyId)
            .ToDictionary(
                g => g.Key,
                g => g.Where(t => t.TransactionType == "BUY").Sum(t => t.Quantity) -
                     g.Where(t => t.TransactionType == "SELL").Sum(t => t.Quantity)
            );

        // 3. Load sector lookup for reliable sector distribution
        var sectors = await _context.Sectors.AsNoTracking().ToListAsync(cancellationToken);
        var sectorMap = sectors.ToDictionary(s => s.SectorId, s => s.SectorName);

        // 4. Query all relevant dividends joined with Company
        var query = _context.Dividends
            .AsNoTracking()
            .Include(d => d.Company)
            .AsQueryable();

        if (filter.CompanyId.HasValue && filter.CompanyId.Value > 0)
        {
            query = query.Where(d => d.CompanyId == filter.CompanyId.Value);
        }

        if (filter.Year.HasValue && filter.Year.Value > 0)
        {
            query = query.Where(d => d.PaymentDate.Year == filter.Year.Value);
        }

        if (filter.StartDate.HasValue)
        {
            var start = filter.StartDate.Value.Date;
            query = query.Where(d => d.PaymentDate >= start);
        }

        if (filter.EndDate.HasValue)
        {
            var end = filter.EndDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(d => d.PaymentDate <= end);
        }

        var dividends = await query
            .OrderByDescending(d => d.PaymentDate)
            .ThenByDescending(d => d.DividendId)
            .ToListAsync(cancellationToken);

        var today = DateTime.UtcNow.Date;
        var currentYear = today.Year;
        var currentMonth = today.Month;

        var historyItems = new List<DividendDto>();
        decimal totalIncome = 0m;
        decimal thisYearIncome = 0m;
        decimal thisMonthIncome = 0m;
        decimal upcomingIncome = 0m;

        foreach (var d in dividends)
        {
            var shares = userSharesMap.TryGetValue(d.CompanyId, out var s) ? Math.Max(0, s) : 0;
            var income = Math.Round(shares * d.DividendPerShare, 2);
            var isPaid = d.PaymentDate.Date <= today;

            totalIncome += income;

            if (d.PaymentDate.Year == currentYear)
            {
                thisYearIncome += income;
            }

            if (d.PaymentDate.Year == currentYear && d.PaymentDate.Month == currentMonth)
            {
                thisMonthIncome += income;
            }

            if (d.PaymentDate.Date > today)
            {
                upcomingIncome += income;
            }

            historyItems.Add(new DividendDto
            {
                DividendId = d.DividendId,
                CompanyId = d.CompanyId,
                CompanyName = d.Company?.CompanyName ?? string.Empty,
                TickerSymbol = d.Company?.TickerSymbol ?? string.Empty,
                DividendPerShare = d.DividendPerShare,
                DeclarationDate = d.DeclarationDate,
                PaymentDate = d.PaymentDate,
                Status = isPaid ? "Paid" : "Upcoming",
                UserSharesHeld = shares,
                EstimatedIncome = income
            });
        }

        // 4. Monthly Income aggregation
        int targetYear = filter.Year ?? currentYear;
        var monthlyBuckets = GenerateEmptyMonthlyBuckets(targetYear);
        var targetYearDividends = historyItems.Where(h => h.PaymentDate.Year == targetYear);

        foreach (var h in targetYearDividends)
        {
            var bucket = monthlyBuckets.FirstOrDefault(m => m.Month == h.PaymentDate.Month);
            if (bucket != null)
            {
                bucket.Income += h.EstimatedIncome;
                bucket.PaymentsCount++;
            }
        }
        foreach (var b in monthlyBuckets)
        {
            b.Income = Math.Round(b.Income, 2);
        }

        // 5. Yearly Income aggregation
        var yearlyIncome = historyItems
            .GroupBy(h => h.PaymentDate.Year)
            .OrderByDescending(g => g.Key)
            .Select(g => new YearlyDividendIncomeDto
            {
                Year = g.Key,
                Income = Math.Round(g.Sum(x => x.EstimatedIncome), 2),
                PaymentsCount = g.Count()
            })
            .ToList();

        // 6. Company Breakdown
        var companyBreakdown = historyItems
            .Where(h => h.CompanyId > 0)
            .GroupBy(h => h.CompanyId)
            .Select(g =>
            {
                var first = g.First();
                var comp = dividends.FirstOrDefault(d => d.CompanyId == g.Key)?.Company;
                var compIncome = Math.Round(g.Sum(x => x.EstimatedIncome), 2);
                var sharesHeld = first.UserSharesHeld;
                var currentPrice = comp?.CurrentPrice ?? 0m;
                var marketVal = Math.Round(sharesHeld * currentPrice, 2);
                var totalDivPerShare = g.Sum(x => x.DividendPerShare);
                var yieldPct = currentPrice > 0 ? Math.Round((totalDivPerShare / currentPrice) * 100, 2) : 0m;
                var allocPct = totalIncome > 0 ? Math.Round((compIncome / totalIncome) * 100, 2) : 0m;

                return new CompanyDividendAnalyticsDto
                {
                    CompanyId = g.Key,
                    TickerSymbol = first.TickerSymbol,
                    CompanyName = first.CompanyName,
                    SectorName = (comp != null && sectorMap.TryGetValue(comp.SectorId, out var sName))
                        ? sName
                        : (comp?.Sector?.SectorName ?? "Unclassified"),
                    TotalIncome = compIncome,
                    TotalDividendPerShare = totalDivPerShare,
                    UserSharesHeld = sharesHeld,
                    CurrentPrice = currentPrice,
                    MarketValue = marketVal,
                    DividendYield = yieldPct,
                    AllocationPercentage = allocPct,
                    PaymentsCount = g.Count()
                };
            })
            .OrderByDescending(c => c.TotalIncome)
            .ThenBy(c => c.TickerSymbol)
            .ToList();

        // 7. Sector Breakdown
        var sectorBreakdown = companyBreakdown
            .GroupBy(c => c.SectorName)
            .Select(sg =>
            {
                var secIncome = Math.Round(sg.Sum(x => x.TotalIncome), 2);
                var allocPct = totalIncome > 0 ? Math.Round((secIncome / totalIncome) * 100, 2) : 0m;
                return new SectorDividendAnalyticsDto
                {
                    SectorName = sg.Key,
                    TotalIncome = secIncome,
                    AllocationPercentage = allocPct,
                    PaymentsCount = sg.Sum(x => x.PaymentsCount),
                    CompaniesCount = sg.Count()
                };
            })
            .OrderByDescending(s => s.TotalIncome)
            .ToList();

        // 8. Upcoming Dividends (Calendar / Timeline)
        var upcomingEvents = dividends
            .Where(d => d.PaymentDate.Date > today)
            .OrderBy(d => d.PaymentDate)
            .Select(d =>
            {
                var shares = userSharesMap.TryGetValue(d.CompanyId, out var s) ? Math.Max(0, s) : 0;
                var estIncome = Math.Round(shares * d.DividendPerShare, 2);
                var daysRemaining = (d.PaymentDate.Date - today).Days;

                return new UpcomingDividendEventDto
                {
                    DividendId = d.DividendId,
                    CompanyId = d.CompanyId,
                    TickerSymbol = d.Company?.TickerSymbol ?? string.Empty,
                    CompanyName = d.Company?.CompanyName ?? string.Empty,
                    DividendPerShare = d.DividendPerShare,
                    DeclarationDate = d.DeclarationDate,
                    PaymentDate = d.PaymentDate,
                    UserSharesHeld = shares,
                    EstimatedIncome = estIncome,
                    DaysUntilPayment = daysRemaining
                };
            })
            .ToList();

        // 9. Overall Portfolio Dividend Yield
        decimal totalPortfolioMarketValue = 0m;
        foreach (var (compId, shares) in userSharesMap)
        {
            if (shares > 0)
            {
                var comp = dividends.FirstOrDefault(d => d.CompanyId == compId)?.Company 
                    ?? await _context.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == compId, cancellationToken);
                if (comp != null)
                {
                    totalPortfolioMarketValue += shares * comp.CurrentPrice;
                }
            }
        }

        var avgYield = totalPortfolioMarketValue > 0
            ? Math.Round((totalIncome / totalPortfolioMarketValue) * 100, 2)
            : 0m;

        var resultDto = new DividendAnalyticsDto
        {
            PortfolioId = filter.PortfolioId,
            PortfolioName = contextName,
            TotalDividendIncome = Math.Round(totalIncome, 2),
            ThisYearIncome = Math.Round(thisYearIncome, 2),
            ThisMonthIncome = Math.Round(thisMonthIncome, 2),
            UpcomingIncome = Math.Round(upcomingIncome, 2),
            AverageDividendYield = avgYield,
            TotalPaymentsCount = historyItems.Count,
            CompaniesCount = companyBreakdown.Count(),
            MonthlyIncome = monthlyBuckets,
            YearlyIncome = yearlyIncome,
            CompanyBreakdown = companyBreakdown,
            SectorBreakdown = sectorBreakdown,
            UpcomingDividends = upcomingEvents,
            DividendHistory = historyItems
        };

        return ApiResponse<DividendAnalyticsDto>.Ok(resultDto);
    }

    private static List<MonthlyDividendIncomeDto> GenerateEmptyMonthlyBuckets(int year)
    {
        var months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        var list = new List<MonthlyDividendIncomeDto>();
        for (int m = 1; m <= 12; m++)
        {
            list.Add(new MonthlyDividendIncomeDto
            {
                Year = year,
                Month = m,
                MonthName = months[m - 1],
                Income = 0m,
                PaymentsCount = 0
            });
        }
        return list;
    }
}
