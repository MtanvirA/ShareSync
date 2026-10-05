using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Goals;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Services;

public class PortfolioGoalService : IPortfolioGoalService
{
    private readonly IApplicationDbContext _context;

    public PortfolioGoalService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<PortfolioGoalDto>>> GetUserGoalsAsync(
        int userId,
        int? portfolioId = null,
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new UnauthorizedException("User session is invalid.");
        }

        List<PortfolioGoal> goals;
        try
        {
            // Query goals belonging strictly to this user
            var query = _context.PortfolioGoals
                .AsNoTracking()
                .Include(g => g.Portfolio)
                .Where(g => g.UserId == userId);

            if (portfolioId.HasValue && portfolioId > 0)
            {
                query = query.Where(g => g.PortfolioId == portfolioId.Value);
            }

            if (activeOnly)
            {
                query = query.Where(g => g.IsActive);
            }

            goals = await query
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync(cancellationToken);
        }
        catch (Exception)
        {
            // If table does not exist or database integrity check encounters unmigrated schema, return clean empty list
            return ApiResponse<List<PortfolioGoalDto>>.Ok(new List<PortfolioGoalDto>());
        }

        if (!goals.Any())
        {
            return ApiResponse<List<PortfolioGoalDto>>.Ok(new List<PortfolioGoalDto>());
        }

        var portfolioIds = goals.Select(g => g.PortfolioId).Distinct().ToList();

        // Load authoritative transaction & dividend data for progress calculation
        var transactions = await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Company)
            .Where(t => portfolioIds.Contains(t.PortfolioId) && t.Company != null)
            .ToListAsync(cancellationToken);

        var dividends = await _context.Dividends
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dtos = new List<PortfolioGoalDto>();
        foreach (var goal in goals)
        {
            var pTxs = transactions.Where(t => t.PortfolioId == goal.PortfolioId).ToList();
            var dto = CalculateGoalProgress(goal, pTxs, dividends);
            dtos.Add(dto);
        }

        return ApiResponse<List<PortfolioGoalDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<PortfolioGoalDto>> GetGoalByIdAsync(
        int goalId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new UnauthorizedException("User session is invalid.");
        }

        var goal = await _context.PortfolioGoals
            .AsNoTracking()
            .Include(g => g.Portfolio)
            .FirstOrDefaultAsync(g => g.GoalId == goalId, cancellationToken);

        if (goal == null)
        {
            throw new NotFoundException("PortfolioGoal", goalId);
        }

        // User isolation
        if (goal.UserId != userId)
        {
            throw new AppException("You do not have permission to view this goal.", 403);
        }

        var transactions = await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Company)
            .Where(t => t.PortfolioId == goal.PortfolioId && t.Company != null)
            .ToListAsync(cancellationToken);

        var dividends = await _context.Dividends
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dto = CalculateGoalProgress(goal, transactions, dividends);
        return ApiResponse<PortfolioGoalDto>.Ok(dto);
    }

    public async Task<ApiResponse<PortfolioGoalDto>> CreateGoalAsync(
        CreatePortfolioGoalRequestDto request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new UnauthorizedException("User session is invalid.");
        }

        if (request == null)
        {
            throw new AppException("Goal request payload cannot be empty.", 400);
        }

        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new AppException("Goal title is required.", 400);
        }

        if (request.TargetValue <= 0)
        {
            throw new AppException("Target value must be greater than zero.", 400);
        }

        if (request.TargetDate.HasValue && request.TargetDate.Value.Date < DateTime.UtcNow.Date)
        {
            throw new AppException("Target date cannot be in the past.", 400);
        }

        // Validate portfolio existence and ownership
        var portfolio = await _context.Portfolios
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PortfolioId == request.PortfolioId, cancellationToken);

        if (portfolio == null)
        {
            throw new NotFoundException("Portfolio", request.PortfolioId);
        }

        if (portfolio.UserId != userId)
        {
            throw new AppException("You do not have permission to attach goals to this portfolio.", 403);
        }

        // Validate goal type
        var goalType = request.GoalType?.Trim().ToUpperInvariant();
        if (goalType != "TARGET_PORTFOLIO_VALUE" &&
            goalType != "TARGET_RETURN" &&
            goalType != "TARGET_DIVIDEND_INCOME")
        {
            throw new AppException("Invalid goal type. Valid types are: TARGET_PORTFOLIO_VALUE, TARGET_RETURN, TARGET_DIVIDEND_INCOME.", 400);
        }

        var goal = new PortfolioGoal
        {
            UserId = userId,
            PortfolioId = request.PortfolioId,
            GoalType = goalType,
            TargetValue = Math.Round(request.TargetValue, 2),
            TargetDate = request.TargetDate?.Date,
            Title = title,
            Description = request.Description?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.PortfolioGoals.Add(goal);
        await _context.SaveChangesAsync(cancellationToken);

        // Calculate progress dynamically
        var transactions = await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Company)
            .Where(t => t.PortfolioId == goal.PortfolioId && t.Company != null)
            .ToListAsync(cancellationToken);

        var dividends = await _context.Dividends
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dto = CalculateGoalProgress(goal, transactions, dividends, portfolio.PortfolioName);

        return ApiResponse<PortfolioGoalDto>.Ok(dto, "Portfolio goal created successfully.");
    }

    public async Task<ApiResponse<PortfolioGoalDto>> UpdateGoalAsync(
        int goalId,
        UpdatePortfolioGoalRequestDto request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new UnauthorizedException("User session is invalid.");
        }

        var goal = await _context.PortfolioGoals
            .Include(g => g.Portfolio)
            .FirstOrDefaultAsync(g => g.GoalId == goalId, cancellationToken);

        if (goal == null)
        {
            throw new NotFoundException("PortfolioGoal", goalId);
        }

        if (goal.UserId != userId)
        {
            throw new AppException("You do not have permission to modify this goal.", 403);
        }

        if (request.TargetValue <= 0)
        {
            throw new AppException("Target value must be greater than zero.", 400);
        }

        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new AppException("Goal title cannot be empty.", 400);
        }

        if (request.TargetDate.HasValue && request.TargetDate.Value.Date < DateTime.UtcNow.Date)
        {
            throw new AppException("Target date cannot be in the past.", 400);
        }

        goal.Title = title;
        goal.Description = request.Description?.Trim();
        goal.TargetValue = Math.Round(request.TargetValue, 2);
        goal.TargetDate = request.TargetDate?.Date;
        if (request.IsActive.HasValue)
        {
            goal.IsActive = request.IsActive.Value;
        }
        goal.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var transactions = await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Company)
            .Where(t => t.PortfolioId == goal.PortfolioId && t.Company != null)
            .ToListAsync(cancellationToken);

        var dividends = await _context.Dividends
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dto = CalculateGoalProgress(goal, transactions, dividends);
        return ApiResponse<PortfolioGoalDto>.Ok(dto, "Portfolio goal updated successfully.");
    }

    public async Task<ApiResponse> DeleteGoalAsync(
        int goalId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new UnauthorizedException("User session is invalid.");
        }

        var goal = await _context.PortfolioGoals
            .FirstOrDefaultAsync(g => g.GoalId == goalId, cancellationToken);

        if (goal == null)
        {
            throw new NotFoundException("PortfolioGoal", goalId);
        }

        if (goal.UserId != userId)
        {
            throw new AppException("You do not have permission to delete this goal.", 403);
        }

        _context.PortfolioGoals.Remove(goal);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse.Ok("Portfolio goal deleted successfully.");
    }

    private static PortfolioGoalDto CalculateGoalProgress(
        PortfolioGoal goal,
        List<Transaction> transactions,
        List<Dividend> dividends,
        string? portfolioName = null)
    {
        // 1. Authoritative calculation of holdings for this portfolio
        var grouped = transactions.GroupBy(t => t.CompanyId);

        decimal portfolioTotalMarketVal = 0m;
        decimal portfolioTotalInvestedVal = 0m;
        var portfolioSharesMap = new Dictionary<int, decimal>();

        foreach (var g in grouped)
        {
            var comp = g.First().Company!;
            var buyTxs = g.Where(t => t.TransactionType == "BUY").ToList();
            var sellTxs = g.Where(t => t.TransactionType == "SELL").ToList();

            var buyQty = buyTxs.Sum(t => t.Quantity);
            var sellQty = sellTxs.Sum(t => t.Quantity);
            var netQty = buyQty - sellQty;

            if (netQty <= 0) continue;

            portfolioSharesMap[g.Key] = netQty;

            var totalBuyCost = buyTxs.Sum(t => t.Quantity * t.PricePerShare);
            var avgBuy = buyQty > 0 ? totalBuyCost / buyQty : 0m;
            var mVal = netQty * comp.CurrentPrice;
            var invVal = netQty * avgBuy;

            portfolioTotalMarketVal += mVal;
            portfolioTotalInvestedVal += invVal;
        }

        decimal portfolioUnrealizedPL = portfolioTotalMarketVal - portfolioTotalInvestedVal;

        // Dividend Income for this portfolio
        decimal portfolioDividendIncome = 0m;
        foreach (var d in dividends)
        {
            if (portfolioSharesMap.TryGetValue(d.CompanyId, out var shares) && shares > 0)
            {
                portfolioDividendIncome += shares * d.DividendPerShare;
            }
        }

        // 2. Determine Current Metric by Goal Type
        decimal currentVal = 0m;
        switch (goal.GoalType)
        {
            case "TARGET_PORTFOLIO_VALUE":
                currentVal = portfolioTotalMarketVal;
                break;
            case "TARGET_RETURN":
                currentVal = portfolioUnrealizedPL;
                break;
            case "TARGET_DIVIDEND_INCOME":
                currentVal = portfolioDividendIncome;
                break;
            default:
                currentVal = portfolioTotalMarketVal;
                break;
        }

        currentVal = Math.Round(currentVal, 2);

        // 3. Progress Percentage & Remaining
        decimal progressPct = 0m;
        if (goal.TargetValue > 0)
        {
            progressPct = Math.Round((currentVal / goal.TargetValue) * 100m, 2);
        }

        decimal remaining = Math.Max(0, Math.Round(goal.TargetValue - currentVal, 2));

        // 4. Status Determination
        string status;
        if (progressPct >= 100m)
        {
            status = "ACHIEVED";
        }
        else if (goal.TargetDate.HasValue && goal.TargetDate.Value.Date < DateTime.UtcNow.Date)
        {
            status = "AT_RISK"; // Target deadline elapsed without meeting threshold
        }
        else if (goal.TargetDate.HasValue)
        {
            var totalDays = (goal.TargetDate.Value.Date - goal.CreatedAt.Date).TotalDays;
            var elapsedDays = (DateTime.UtcNow.Date - goal.CreatedAt.Date).TotalDays;

            if (totalDays > 0)
            {
                var timePace = (decimal)(elapsedDays / totalDays) * 100m;
                // If over 25% of time has elapsed and progress is less than half the expected time pace
                if (timePace > 25m && progressPct < (timePace * 0.5m))
                {
                    status = "AT_RISK";
                }
                else
                {
                    status = "ON_TRACK";
                }
            }
            else
            {
                status = "ON_TRACK";
            }
        }
        else
        {
            status = "ON_TRACK";
        }

        return new PortfolioGoalDto
        {
            GoalId = goal.GoalId,
            UserId = goal.UserId,
            PortfolioId = goal.PortfolioId,
            PortfolioName = portfolioName ?? goal.Portfolio?.PortfolioName ?? "Portfolio",
            GoalType = goal.GoalType,
            Title = goal.Title,
            Description = goal.Description,
            TargetValue = goal.TargetValue,
            CurrentValue = currentVal,
            ProgressPercentage = progressPct,
            RemainingValue = remaining,
            TargetDate = goal.TargetDate,
            Status = status,
            IsActive = goal.IsActive,
            CreatedAt = goal.CreatedAt,
            UpdatedAt = goal.UpdatedAt
        };
    }
}
