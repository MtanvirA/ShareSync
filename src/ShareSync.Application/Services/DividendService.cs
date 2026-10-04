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
}
