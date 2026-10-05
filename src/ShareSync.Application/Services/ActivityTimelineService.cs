using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Activities;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Services;

public class ActivityTimelineService : IActivityTimelineService
{
    private readonly IApplicationDbContext _context;

    public ActivityTimelineService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<PagedActivitiesDto>> GetUserTimelineAsync(
        int userId,
        UserActivityFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        if (filter.StartDate.HasValue && filter.EndDate.HasValue && filter.StartDate.Value > filter.EndDate.Value)
        {
            throw new AppException("Start date cannot be after end date.", 400);
        }

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        // 1. Retrieve user's accessible portfolios
        var userPortfolios = await _context.Portfolios
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        var userPortfolioIds = userPortfolios.Select(p => p.PortfolioId).ToList();

        var activities = new List<UserActivityDto>();

        // 2. Existing Transaction Audits
        var userAudits = await _context.TransactionAudits
            .AsNoTracking()
            .Include(a => a.Transaction)
                .ThenInclude(t => t!.Company)
            .Include(a => a.Transaction)
                .ThenInclude(t => t!.Portfolio)
            .Where(a => (a.Transaction != null && userPortfolioIds.Contains(a.Transaction.PortfolioId))
                     || a.ChangedBy == $"User #{userId}")
            .ToListAsync(cancellationToken);

        var auditedInsertTxIds = new HashSet<int>();

        foreach (var audit in userAudits)
        {
            if (audit.ActionType == "INSERT")
            {
                if (audit.TransactionId.HasValue)
                {
                    auditedInsertTxIds.Add(audit.TransactionId.Value);
                }

                if (audit.Transaction != null)
                {
                    var isBuy = audit.Transaction.TransactionType == "BUY";
                    var ticker = audit.Transaction.Company?.TickerSymbol ?? "stock";
                    var pName = audit.Transaction.Portfolio?.PortfolioName ?? "Portfolio";

                    activities.Add(new UserActivityDto
                    {
                        ActivityId = $"audit_{audit.AuditId}",
                        EventType = isBuy ? "BUY" : "SELL",
                        Title = $"{(isBuy ? "Bought" : "Sold")} {audit.Transaction.Quantity:N0} shares of {ticker}",
                        Description = $"Price: ৳{audit.Transaction.PricePerShare:N2} | Total: ৳{audit.Transaction.Quantity * audit.Transaction.PricePerShare:N2} ({pName})",
                        Timestamp = audit.ActionDate,
                        RelatedEntityType = "Transaction",
                        RelatedEntityId = audit.TransactionId,
                        Icon = isBuy ? "bi-arrow-down-left" : "bi-arrow-up-right",
                        BadgeClass = isBuy ? "buy" : "sell"
                    });
                }
                else
                {
                    var isBuy = (audit.Details ?? "").Contains("BUY", StringComparison.OrdinalIgnoreCase);
                    activities.Add(new UserActivityDto
                    {
                        ActivityId = $"audit_{audit.AuditId}",
                        EventType = isBuy ? "BUY" : "SELL",
                        Title = audit.Details ?? "Recorded transaction",
                        Description = audit.Details ?? "Transaction recorded in portfolio",
                        Timestamp = audit.ActionDate,
                        RelatedEntityType = "Transaction",
                        RelatedEntityId = audit.TransactionId,
                        Icon = isBuy ? "bi-arrow-down-left" : "bi-arrow-up-right",
                        BadgeClass = isBuy ? "buy" : "sell"
                    });
                }
            }
            else if (audit.ActionType == "UPDATE")
            {
                activities.Add(new UserActivityDto
                {
                    ActivityId = $"audit_{audit.AuditId}",
                    EventType = "TRANSACTION_UPDATED",
                    Title = audit.Transaction?.Company != null
                        ? $"Updated transaction for {audit.Transaction.Company.TickerSymbol}"
                        : $"Updated transaction #{audit.TransactionId}",
                    Description = audit.Details ?? "Transaction details updated",
                    Timestamp = audit.ActionDate,
                    RelatedEntityType = "Transaction",
                    RelatedEntityId = audit.TransactionId,
                    Icon = "bi-pencil-square",
                    BadgeClass = "warning"
                });
            }
            else if (audit.ActionType == "DELETE")
            {
                activities.Add(new UserActivityDto
                {
                    ActivityId = $"audit_{audit.AuditId}",
                    EventType = "TRANSACTION_DELETED",
                    Title = $"Deleted transaction #{audit.TransactionId}",
                    Description = audit.Details ?? "Transaction deleted from portfolio",
                    Timestamp = audit.ActionDate,
                    RelatedEntityType = "Transaction",
                    RelatedEntityId = audit.TransactionId,
                    Icon = "bi-trash",
                    BadgeClass = "danger"
                });
            }
        }

        // 3. Transactions not captured by audit triggers (e.g., in tests or legacy seeds)
        var userTransactions = await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Company)
            .Include(t => t.Portfolio)
            .Where(t => userPortfolioIds.Contains(t.PortfolioId))
            .ToListAsync(cancellationToken);

        foreach (var t in userTransactions)
        {
            if (!auditedInsertTxIds.Contains(t.TransactionId))
            {
                var isBuy = t.TransactionType == "BUY";
                var ticker = t.Company?.TickerSymbol ?? "stock";
                var pName = t.Portfolio?.PortfolioName ?? "Portfolio";

                activities.Add(new UserActivityDto
                {
                    ActivityId = $"tx_{t.TransactionId}",
                    EventType = isBuy ? "BUY" : "SELL",
                    Title = $"{(isBuy ? "Bought" : "Sold")} {t.Quantity:N0} shares of {ticker}",
                    Description = $"Price: ৳{t.PricePerShare:N2} | Total: ৳{t.Quantity * t.PricePerShare:N2} ({pName})",
                    Timestamp = t.TransactionDate,
                    RelatedEntityType = "Transaction",
                    RelatedEntityId = t.TransactionId,
                    Icon = isBuy ? "bi-arrow-down-left" : "bi-arrow-up-right",
                    BadgeClass = isBuy ? "buy" : "sell"
                });
            }
        }

        // 4. Portfolio Created Events
        foreach (var p in userPortfolios)
        {
            activities.Add(new UserActivityDto
            {
                ActivityId = $"portfolio_create_{p.PortfolioId}",
                EventType = "PORTFOLIO_CREATED",
                Title = $"Created \"{p.PortfolioName}\"",
                Description = string.IsNullOrWhiteSpace(p.Description)
                    ? $"Created portfolio {p.PortfolioName}"
                    : p.Description,
                Timestamp = p.CreatedAt,
                RelatedEntityType = "Portfolio",
                RelatedEntityId = p.PortfolioId,
                Icon = "bi-briefcase",
                BadgeClass = "primary"
            });
        }

        // 5. Watchlist Added Events
        var watchlistItems = await _context.WatchlistItems
            .AsNoTracking()
            .Include(w => w.Watchlist)
            .Include(w => w.Company)
            .Where(w => w.Watchlist != null && w.Watchlist.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var item in watchlistItems)
        {
            var ticker = item.Company?.TickerSymbol ?? "stock";
            activities.Add(new UserActivityDto
            {
                ActivityId = $"watchlist_add_{item.WatchlistId}_{item.CompanyId}",
                EventType = "WATCHLIST_ADDED",
                Title = $"Added {ticker} to Watchlist",
                Description = item.TargetPrice.HasValue
                    ? $"Target price set to ৳{item.TargetPrice.Value:N2} in {item.Watchlist?.WatchlistName}"
                    : $"Added {item.Company?.CompanyName} to {item.Watchlist?.WatchlistName}",
                Timestamp = item.AddedAt,
                RelatedEntityType = "Company",
                RelatedEntityId = item.CompanyId,
                Icon = "bi-bookmark-plus",
                BadgeClass = "info"
            });
        }

        // 6. Portfolio Goal Events
        List<PortfolioGoal> goals;
        try
        {
            goals = await _context.PortfolioGoals
                .AsNoTracking()
                .Include(g => g.Portfolio)
                .Where(g => g.UserId == userId)
                .ToListAsync(cancellationToken);
        }
        catch
        {
            goals = new List<PortfolioGoal>();
        }

        foreach (var g in goals)
        {
            activities.Add(new UserActivityDto
            {
                ActivityId = $"goal_create_{g.GoalId}",
                EventType = "GOAL_CREATED",
                Title = $"Created goal \"{g.Title}\"",
                Description = $"Target {g.GoalType}: ৳{g.TargetValue:N2} for portfolio {g.Portfolio?.PortfolioName}",
                Timestamp = g.CreatedAt,
                RelatedEntityType = "PortfolioGoal",
                RelatedEntityId = g.GoalId,
                Icon = "bi-bullseye",
                BadgeClass = "warning"
            });

            if (g.UpdatedAt.HasValue && g.UpdatedAt.Value > g.CreatedAt.AddSeconds(5))
            {
                activities.Add(new UserActivityDto
                {
                    ActivityId = $"goal_update_{g.GoalId}_{g.UpdatedAt.Value.Ticks}",
                    EventType = "GOAL_UPDATED",
                    Title = $"Updated goal \"{g.Title}\"",
                    Description = $"Target value updated to ৳{g.TargetValue:N2}",
                    Timestamp = g.UpdatedAt.Value,
                    RelatedEntityType = "PortfolioGoal",
                    RelatedEntityId = g.GoalId,
                    Icon = "bi-bullseye",
                    BadgeClass = "warning"
                });
            }
        }

        // 7. Alert Events
        var alerts = await _context.Alerts
            .AsNoTracking()
            .Include(a => a.Company)
            .Include(a => a.Portfolio)
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var a in alerts)
        {
            activities.Add(new UserActivityDto
            {
                ActivityId = $"alert_create_{a.AlertId}",
                EventType = "ALERT_CREATED",
                Title = $"Created {a.AlertType} alert" + (a.Company != null ? $" for {a.Company.TickerSymbol}" : ""),
                Description = a.Message ?? $"Target threshold set at ৳{a.ThresholdValue:N2}",
                Timestamp = a.CreatedAt,
                RelatedEntityType = "Alert",
                RelatedEntityId = a.AlertId,
                Icon = "bi-bell",
                BadgeClass = "warning"
            });

            if (a.TriggeredAt.HasValue)
            {
                activities.Add(new UserActivityDto
                {
                    ActivityId = $"alert_trigger_{a.AlertId}_{a.TriggeredAt.Value.Ticks}",
                    EventType = "ALERT_TRIGGERED",
                    Title = $"Alert triggered: {a.AlertType}" + (a.Company != null ? $" for {a.Company.TickerSymbol}" : ""),
                    Description = a.Message ?? $"Trigger condition reached at ৳{a.ThresholdValue:N2}",
                    Timestamp = a.TriggeredAt.Value,
                    RelatedEntityType = "Alert",
                    RelatedEntityId = a.AlertId,
                    Icon = "bi-bell-fill",
                    BadgeClass = "danger"
                });
            }
        }

        // 8. Dividend Events
        var userCompanyIds = userTransactions.Select(t => t.CompanyId).Distinct().ToList();
        if (userCompanyIds.Count > 0)
        {
            var dividends = await _context.Dividends
                .AsNoTracking()
                .Include(d => d.Company)
                .Where(d => userCompanyIds.Contains(d.CompanyId))
                .ToListAsync(cancellationToken);

            foreach (var d in dividends)
            {
                var qty = userTransactions
                    .Where(t => t.CompanyId == d.CompanyId && t.TransactionDate <= d.PaymentDate)
                    .Sum(t => t.TransactionType == "BUY" ? t.Quantity : -t.Quantity);

                if (qty > 0)
                {
                    var totalDiv = qty * d.DividendPerShare;
                    activities.Add(new UserActivityDto
                    {
                        ActivityId = $"dividend_{d.DividendId}",
                        EventType = "DIVIDEND_RECORDED",
                        Title = $"Dividend received from {d.Company?.TickerSymbol ?? "Company"}",
                        Description = $"৳{totalDiv:N2} (৳{d.DividendPerShare:N2}/share on {qty:N0} shares)",
                        Timestamp = d.PaymentDate,
                        RelatedEntityType = "Dividend",
                        RelatedEntityId = d.DividendId,
                        Icon = "bi-cash-coin",
                        BadgeClass = "success"
                    });
                }
            }
        }

        // 9. Apply Filtering
        var filtered = activities.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.EventType) && !filter.EventType.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var typeFilter = filter.EventType.Trim().ToUpperInvariant();
            if (typeFilter == "TRANSACTION")
            {
                filtered = filtered.Where(a => a.EventType is "BUY" or "SELL" or "TRANSACTION_UPDATED" or "TRANSACTION_DELETED");
            }
            else if (typeFilter == "PORTFOLIO")
            {
                filtered = filtered.Where(a => a.EventType.StartsWith("PORTFOLIO", StringComparison.OrdinalIgnoreCase));
            }
            else if (typeFilter == "WATCHLIST")
            {
                filtered = filtered.Where(a => a.EventType.StartsWith("WATCHLIST", StringComparison.OrdinalIgnoreCase));
            }
            else if (typeFilter == "GOAL")
            {
                filtered = filtered.Where(a => a.EventType.StartsWith("GOAL", StringComparison.OrdinalIgnoreCase));
            }
            else if (typeFilter == "ALERT")
            {
                filtered = filtered.Where(a => a.EventType.StartsWith("ALERT", StringComparison.OrdinalIgnoreCase));
            }
            else if (typeFilter == "DIVIDEND")
            {
                filtered = filtered.Where(a => a.EventType.StartsWith("DIVIDEND", StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                filtered = filtered.Where(a => a.EventType.Equals(typeFilter, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (filter.StartDate.HasValue)
        {
            filtered = filtered.Where(a => a.Timestamp >= filter.StartDate.Value);
        }

        if (filter.EndDate.HasValue)
        {
            filtered = filtered.Where(a => a.Timestamp <= filter.EndDate.Value);
        }

        // 10. Order by Timestamp Descending
        var ordered = filtered
            .OrderByDescending(a => a.Timestamp)
            .ThenByDescending(a => a.ActivityId)
            .ToList();

        // 11. Pagination
        var totalItems = ordered.Count;
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var pagedItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var result = new PagedActivitiesDto
        {
            Items = pagedItems,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };

        return ApiResponse<PagedActivitiesDto>.Ok(result, $"Retrieved {pagedItems.Count} activity records.");
    }
}
