using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Snapshots;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Services;

public class PortfolioSnapshotService : IPortfolioSnapshotService
{
    private readonly IApplicationDbContext _context;

    public PortfolioSnapshotService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<PortfolioSnapshotDto>>> GetPortfolioSnapshotsAsync(
        int portfolioId,
        int userId,
        SnapshotFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify portfolio exists and user ownership
        var portfolio = await _context.Portfolios
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PortfolioId == portfolioId, cancellationToken);

        if (portfolio == null)
        {
            throw new NotFoundException("Portfolio", portfolioId);
        }

        if (portfolio.UserId != userId)
        {
            throw new AppException("You do not have permission to access this portfolio.", 403);
        }

        // 2. Query snapshots
        var query = _context.PortfolioSnapshots
            .AsNoTracking()
            .Where(s => s.PortfolioId == portfolioId);

        if (filter?.StartDate.HasValue == true)
        {
            var start = filter.StartDate.Value.Date;
            query = query.Where(s => s.SnapshotDate >= start);
        }

        if (filter?.EndDate.HasValue == true)
        {
            var end = filter.EndDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(s => s.SnapshotDate <= end);
        }

        var snapshots = await query
            .OrderBy(s => s.SnapshotDate)
            .ToListAsync(cancellationToken);

        // 3. Compute changes chronologically
        var dtos = new List<PortfolioSnapshotDto>();
        decimal? previousValue = null;

        foreach (var s in snapshots)
        {
            decimal? valueChange = null;
            decimal? pctChange = null;

            if (previousValue.HasValue)
            {
                valueChange = s.TotalValue - previousValue.Value;
                pctChange = previousValue.Value > 0
                    ? Math.Round((valueChange.Value / previousValue.Value) * 100, 2)
                    : 0;
            }

            dtos.Add(new PortfolioSnapshotDto
            {
                SnapshotId = s.SnapshotId,
                PortfolioId = s.PortfolioId,
                PortfolioName = portfolio.PortfolioName,
                SnapshotDate = s.SnapshotDate,
                TotalValue = s.TotalValue,
                ValueChange = valueChange,
                PercentageChange = pctChange
            });

            previousValue = s.TotalValue;
        }

        return ApiResponse<List<PortfolioSnapshotDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<PortfolioSnapshotDto>> GetSnapshotByIdAsync(
        int snapshotId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _context.PortfolioSnapshots
            .AsNoTracking()
            .Include(s => s.Portfolio)
            .FirstOrDefaultAsync(s => s.SnapshotId == snapshotId, cancellationToken);

        if (snapshot == null)
        {
            throw new NotFoundException("PortfolioSnapshot", snapshotId);
        }

        if (snapshot.Portfolio?.UserId != userId)
        {
            throw new AppException("You do not have permission to access this snapshot.", 403);
        }

        var dto = new PortfolioSnapshotDto
        {
            SnapshotId = snapshot.SnapshotId,
            PortfolioId = snapshot.PortfolioId,
            PortfolioName = snapshot.Portfolio.PortfolioName,
            SnapshotDate = snapshot.SnapshotDate,
            TotalValue = snapshot.TotalValue
        };

        return ApiResponse<PortfolioSnapshotDto>.Ok(dto);
    }

    public async Task<ApiResponse<PortfolioSnapshotDto>> CreateSnapshotAsync(
        int portfolioId,
        CreateSnapshotRequestDto request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (!request.SnapshotDate.HasValue)
        {
            throw new AppException("Snapshot date is required.", 400);
        }

        if (request.TotalValue.HasValue && request.TotalValue.Value < 0)
        {
            throw new AppException("Total value cannot be negative.", 400);
        }

        // 1. Verify portfolio exists and user ownership
        var portfolio = await _context.Portfolios
            .FirstOrDefaultAsync(p => p.PortfolioId == portfolioId, cancellationToken);

        if (portfolio == null)
        {
            throw new NotFoundException("Portfolio", portfolioId);
        }

        if (portfolio.UserId != userId)
        {
            throw new AppException("You do not have permission to access this portfolio.", 403);
        }

        var snapshotDate = request.SnapshotDate.Value.Date;

        // 2. Check unique constraint: UQ_PORTFOLIO_SNAPSHOT_DATE (portfolio_id, snapshot_date)
        var exists = await _context.PortfolioSnapshots
            .AnyAsync(s => s.PortfolioId == portfolioId && s.SnapshotDate == snapshotDate, cancellationToken);

        if (exists)
        {
            throw new AppException($"A snapshot for portfolio '{portfolio.PortfolioName}' on date {snapshotDate:yyyy-MM-dd} already exists.", 409);
        }

        // 3. Determine snapshot total value
        decimal finalValue;
        if (request.TotalValue.HasValue)
        {
            finalValue = request.TotalValue.Value;
        }
        else
        {
            // Calculate value server-side based on holdings as of snapshot date
            finalValue = await CalculateHoldingsMarketValueAsync(portfolioId, snapshotDate, cancellationToken);
        }

        var snapshot = new PortfolioSnapshot
        {
            PortfolioId = portfolioId,
            SnapshotDate = snapshotDate,
            TotalValue = Math.Round(finalValue, 2)
        };

        _context.PortfolioSnapshots.Add(snapshot);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = new PortfolioSnapshotDto
        {
            SnapshotId = snapshot.SnapshotId,
            PortfolioId = snapshot.PortfolioId,
            PortfolioName = portfolio.PortfolioName,
            SnapshotDate = snapshot.SnapshotDate,
            TotalValue = snapshot.TotalValue
        };

        return ApiResponse<PortfolioSnapshotDto>.Ok(dto, "Portfolio snapshot recorded successfully.");
    }

    public async Task<ApiResponse> DeleteSnapshotAsync(
        int snapshotId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _context.PortfolioSnapshots
            .Include(s => s.Portfolio)
            .FirstOrDefaultAsync(s => s.SnapshotId == snapshotId, cancellationToken);

        if (snapshot == null)
        {
            throw new NotFoundException("PortfolioSnapshot", snapshotId);
        }

        if (snapshot.Portfolio?.UserId != userId)
        {
            throw new AppException("You do not have permission to access this snapshot.", 403);
        }

        _context.PortfolioSnapshots.Remove(snapshot);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse.Ok("Portfolio snapshot deleted successfully.");
    }

    public async Task<ApiResponse<PortfolioPerformanceDataDto>> GetPerformanceDataAsync(
        int portfolioId,
        int userId,
        string? period = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify portfolio exists and ownership
        var portfolio = await _context.Portfolios
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PortfolioId == portfolioId, cancellationToken);

        if (portfolio == null)
        {
            throw new NotFoundException("Portfolio", portfolioId);
        }

        if (portfolio.UserId != userId)
        {
            throw new AppException("You do not have permission to access this portfolio.", 403);
        }

        // 2. Apply period filter
        var now = DateTime.UtcNow.Date;
        DateTime? startDate = null;

        if (!string.IsNullOrWhiteSpace(period))
        {
            var pLower = period.Trim().ToLowerInvariant();
            if (pLower.Contains("1 month") || pLower == "1m")
            {
                startDate = now.AddMonths(-1);
            }
            else if (pLower.Contains("6 month") || pLower == "6m")
            {
                startDate = now.AddMonths(-6);
            }
            else if (pLower.Contains("1 year") || pLower == "1y")
            {
                startDate = now.AddYears(-1);
            }
        }

        var query = _context.PortfolioSnapshots
            .AsNoTracking()
            .Where(s => s.PortfolioId == portfolioId);

        if (startDate.HasValue)
        {
            query = query.Where(s => s.SnapshotDate >= startDate.Value);
        }

        var snapshots = await query
            .OrderBy(s => s.SnapshotDate)
            .ToListAsync(cancellationToken);

        var result = new PortfolioPerformanceDataDto
        {
            PortfolioId = portfolio.PortfolioId,
            PortfolioName = portfolio.PortfolioName
        };

        if (snapshots.Any())
        {
            decimal? prevVal = null;
            foreach (var s in snapshots)
            {
                result.Labels.Add(s.SnapshotDate.ToString("MMM dd"));
                result.Values.Add(s.TotalValue);

                decimal? vChange = null;
                decimal? pChange = null;
                if (prevVal.HasValue)
                {
                    vChange = s.TotalValue - prevVal.Value;
                    pChange = prevVal.Value > 0 ? Math.Round((vChange.Value / prevVal.Value) * 100, 2) : 0;
                }

                result.Snapshots.Add(new PortfolioSnapshotDto
                {
                    SnapshotId = s.SnapshotId,
                    PortfolioId = s.PortfolioId,
                    PortfolioName = portfolio.PortfolioName,
                    SnapshotDate = s.SnapshotDate,
                    TotalValue = s.TotalValue,
                    ValueChange = vChange,
                    PercentageChange = pChange
                });

                prevVal = s.TotalValue;
            }

            result.StartingValue = snapshots.First().TotalValue;
            result.CurrentValue = snapshots.Last().TotalValue;
            result.OverallChange = result.CurrentValue - result.StartingValue;
            result.OverallChangePercentage = result.StartingValue > 0
                ? Math.Round((result.OverallChange / result.StartingValue) * 100, 2)
                : 0;
        }
        else
        {
            // If no historical snapshots exist yet, compute current market value as a single baseline point
            var currentVal = await CalculateHoldingsMarketValueAsync(portfolioId, now, cancellationToken);
            result.Labels.Add(now.ToString("MMM dd"));
            result.Values.Add(currentVal);
            result.StartingValue = currentVal;
            result.CurrentValue = currentVal;
            result.OverallChange = 0;
            result.OverallChangePercentage = 0;
        }

        return ApiResponse<PortfolioPerformanceDataDto>.Ok(result);
    }

    private async Task<decimal> CalculateHoldingsMarketValueAsync(
        int portfolioId,
        DateTime asOfDate,
        CancellationToken cancellationToken)
    {
        var endOfDate = asOfDate.Date.AddDays(1).AddTicks(-1);

        var transactions = await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Company)
            .Where(t => t.PortfolioId == portfolioId && t.TransactionDate <= endOfDate)
            .ToListAsync(cancellationToken);

        if (!transactions.Any())
        {
            return 0m;
        }

        decimal total = 0m;
        var groups = transactions.Where(t => t.Company != null).GroupBy(t => t.CompanyId);

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
