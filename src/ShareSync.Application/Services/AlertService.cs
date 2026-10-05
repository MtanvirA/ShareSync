using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Alerts;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Services;

public class AlertService : IAlertService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<AlertService> _logger;
    private readonly INotificationService? _notificationService;

    public AlertService(
        IApplicationDbContext context,
        ILogger<AlertService> logger,
        INotificationService? notificationService = null)
    {
        _context = context;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<ApiResponse<List<AlertDto>>> GetUserAlertsAsync(
        int userId,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Alerts
            .AsNoTracking()
            .Include(a => a.Company)
            .Include(a => a.Portfolio)
            .Where(a => a.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim().ToUpperInvariant();
            if (s == "ACTIVE")
            {
                query = query.Where(a => a.IsActive && a.TriggeredAt == null);
            }
            else if (s == "TRIGGERED")
            {
                query = query.Where(a => a.TriggeredAt != null);
            }
            else if (s == "DISABLED")
            {
                query = query.Where(a => !a.IsActive && a.TriggeredAt == null);
            }
        }

        var alerts = await query
            .OrderByDescending(a => a.TriggeredAt.HasValue)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        // Preload portfolio market values for display
        var userPortfolioIds = alerts.Where(a => a.PortfolioId.HasValue).Select(a => a.PortfolioId!.Value).Distinct().ToList();
        var portfolioValues = await GetPortfolioMarketValuesAsync(userPortfolioIds, cancellationToken);

        var dtos = alerts.Select(a => MapToDto(a, portfolioValues)).ToList();
        return ApiResponse<List<AlertDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<AlertDto>> GetAlertByIdAsync(
        int alertId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var alert = await _context.Alerts
            .AsNoTracking()
            .Include(a => a.Company)
            .Include(a => a.Portfolio)
            .FirstOrDefaultAsync(a => a.AlertId == alertId, cancellationToken);

        if (alert == null)
        {
            throw new NotFoundException("Alert", alertId);
        }

        if (alert.UserId != userId)
        {
            throw new AppException("You do not have permission to access this alert.", 403);
        }

        return ApiResponse<AlertDto>.Ok(MapToDto(alert));
    }

    public async Task<ApiResponse<AlertDto>> CreateAlertAsync(
        CreateAlertRequestDto request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new AppException("Invalid alert request payload.");
        }

        var alertType = request.AlertType?.Trim().ToUpperInvariant() ?? string.Empty;
        if (alertType != "PRICE_ABOVE" && alertType != "PRICE_BELOW" &&
            alertType != "PORTFOLIO_VALUE_ABOVE" && alertType != "PORTFOLIO_VALUE_BELOW")
        {
            throw new AppException("Invalid alert type. Must be PRICE_ABOVE, PRICE_BELOW, PORTFOLIO_VALUE_ABOVE, or PORTFOLIO_VALUE_BELOW.");
        }

        if (request.ThresholdValue <= 0)
        {
            throw new AppException("Threshold value must be greater than zero.");
        }

        int? companyId = null;
        int? portfolioId = null;

        if (alertType == "PRICE_ABOVE" || alertType == "PRICE_BELOW")
        {
            if (!request.CompanyId.HasValue || request.CompanyId.Value <= 0)
            {
                throw new AppException("Company ID is required for price-based alerts.");
            }

            var companyExists = await _context.Companies
                .AsNoTracking()
                .AnyAsync(c => c.CompanyId == request.CompanyId.Value, cancellationToken);

            if (!companyExists)
            {
                throw new NotFoundException("Company", request.CompanyId.Value);
            }

            companyId = request.CompanyId.Value;
        }
        else
        {
            if (!request.PortfolioId.HasValue || request.PortfolioId.Value <= 0)
            {
                throw new AppException("Portfolio ID is required for portfolio threshold alerts.");
            }

            var portfolio = await _context.Portfolios
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PortfolioId == request.PortfolioId.Value, cancellationToken);

            if (portfolio == null)
            {
                throw new NotFoundException("Portfolio", request.PortfolioId.Value);
            }

            if (portfolio.UserId != userId)
            {
                throw new AppException("You do not have permission to configure alerts for this portfolio.", 403);
            }

            portfolioId = request.PortfolioId.Value;
        }

        var threshold = Math.Round(request.ThresholdValue, 2);

        // Duplicate Check: Prevent creating an identical active, untriggered alert
        var isDuplicate = await _context.Alerts
            .AsNoTracking()
            .AnyAsync(a =>
                a.UserId == userId &&
                a.AlertType == alertType &&
                a.CompanyId == companyId &&
                a.PortfolioId == portfolioId &&
                a.ThresholdValue == threshold &&
                a.IsActive &&
                a.TriggeredAt == null,
                cancellationToken);

        if (isDuplicate)
        {
            throw new AppException("An identical active alert already exists with this threshold value.", 409);
        }

        var alert = new Alert
        {
            UserId = userId,
            CompanyId = companyId,
            PortfolioId = portfolioId,
            AlertType = alertType,
            ThresholdValue = threshold,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            TriggeredAt = null,
            Message = null
        };

        _context.Alerts.Add(alert);
        await _context.SaveChangesAsync(cancellationToken);

        // Reload alert with navigation entities
        var created = await _context.Alerts
            .AsNoTracking()
            .Include(a => a.Company)
            .Include(a => a.Portfolio)
            .FirstAsync(a => a.AlertId == alert.AlertId, cancellationToken);

        return ApiResponse<AlertDto>.Ok(MapToDto(created), "Alert created successfully.");
    }

    public async Task<ApiResponse<AlertDto>> UpdateAlertAsync(
        int alertId,
        UpdateAlertRequestDto request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var alert = await _context.Alerts
            .Include(a => a.Company)
            .Include(a => a.Portfolio)
            .FirstOrDefaultAsync(a => a.AlertId == alertId, cancellationToken);

        if (alert == null)
        {
            throw new NotFoundException("Alert", alertId);
        }

        if (alert.UserId != userId)
        {
            throw new AppException("You do not have permission to modify this alert.", 403);
        }

        if (request.ThresholdValue <= 0)
        {
            throw new AppException("Threshold value must be greater than zero.");
        }

        alert.ThresholdValue = Math.Round(request.ThresholdValue, 2);

        if (request.IsActive.HasValue)
        {
            alert.IsActive = request.IsActive.Value;
            if (alert.IsActive)
            {
                // Reactivating an alert clears its previous trigger status
                alert.TriggeredAt = null;
                alert.Message = null;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<AlertDto>.Ok(MapToDto(alert), "Alert updated successfully.");
    }

    public async Task<ApiResponse<AlertDto>> ToggleAlertAsync(
        int alertId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var alert = await _context.Alerts
            .Include(a => a.Company)
            .Include(a => a.Portfolio)
            .FirstOrDefaultAsync(a => a.AlertId == alertId, cancellationToken);

        if (alert == null)
        {
            throw new NotFoundException("Alert", alertId);
        }

        if (alert.UserId != userId)
        {
            throw new AppException("You do not have permission to modify this alert.", 403);
        }

        if (alert.TriggeredAt.HasValue)
        {
            // Reactivate triggered alert
            alert.IsActive = true;
            alert.TriggeredAt = null;
            alert.Message = null;
        }
        else
        {
            alert.IsActive = !alert.IsActive;
        }

        await _context.SaveChangesAsync(cancellationToken);

        string action = alert.IsActive ? "activated" : "disabled";
        return ApiResponse<AlertDto>.Ok(MapToDto(alert), $"Alert {action} successfully.");
    }

    public async Task<ApiResponse<bool>> DeleteAlertAsync(
        int alertId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var alert = await _context.Alerts
            .FirstOrDefaultAsync(a => a.AlertId == alertId, cancellationToken);

        if (alert == null)
        {
            throw new NotFoundException("Alert", alertId);
        }

        if (alert.UserId != userId)
        {
            throw new AppException("You do not have permission to delete this alert.", 403);
        }

        _context.Alerts.Remove(alert);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, "Alert deleted successfully.");
    }

    public async Task<int> EvaluatePriceAlertsAsync(CancellationToken cancellationToken = default)
    {
        // Query active, untriggered price alerts
        var activeAlerts = await _context.Alerts
            .Include(a => a.Company)
            .Where(a => a.IsActive && a.TriggeredAt == null && (a.AlertType == "PRICE_ABOVE" || a.AlertType == "PRICE_BELOW"))
            .ToListAsync(cancellationToken);

        if (!activeAlerts.Any())
        {
            return 0;
        }

        int triggeredCount = 0;
        var now = DateTime.UtcNow;

        foreach (var alert in activeAlerts)
        {
            if (alert.Company == null) continue;

            var price = alert.Company.CurrentPrice;
            bool triggered = false;
            string msg = string.Empty;

            if (alert.AlertType == "PRICE_ABOVE" && price >= alert.ThresholdValue)
            {
                triggered = true;
                msg = $"{alert.Company.TickerSymbol} reached ৳{price:N2}, exceeding your target threshold of ৳{alert.ThresholdValue:N2}.";
            }
            else if (alert.AlertType == "PRICE_BELOW" && price <= alert.ThresholdValue)
            {
                triggered = true;
                msg = $"{alert.Company.TickerSymbol} fell to ৳{price:N2}, dropping below your threshold of ৳{alert.ThresholdValue:N2}.";
            }

            if (triggered)
            {
                alert.TriggeredAt = now;
                alert.IsActive = false; // Prevent repeated evaluation until user reactivates
                alert.Message = msg;
                triggeredCount++;

                _logger.LogInformation("Price Alert {AlertId} Triggered: {Message}", alert.AlertId, msg);

                if (_notificationService != null)
                {
                    await _notificationService.CreateNotificationFromAlertAsync(
                        alert,
                        title: $"Price Alert: {alert.Company?.TickerSymbol ?? "Stock"}",
                        message: msg,
                        cancellationToken);
                }
            }
        }

        if (triggeredCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return triggeredCount;
    }

    public async Task<int> EvaluatePortfolioAlertsAsync(
        int? portfolioId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Alerts
            .Include(a => a.Portfolio)
            .Where(a => a.IsActive && a.TriggeredAt == null && (a.AlertType == "PORTFOLIO_VALUE_ABOVE" || a.AlertType == "PORTFOLIO_VALUE_BELOW"));

        if (portfolioId.HasValue)
        {
            query = query.Where(a => a.PortfolioId == portfolioId.Value);
        }

        var activeAlerts = await query.ToListAsync(cancellationToken);
        if (!activeAlerts.Any())
        {
            return 0;
        }

        var targetPortfolioIds = activeAlerts.Select(a => a.PortfolioId!.Value).Distinct().ToList();

        // Calculate current market value for each portfolio
        var portfolioValues = await GetPortfolioMarketValuesAsync(targetPortfolioIds, cancellationToken);

        int triggeredCount = 0;
        var now = DateTime.UtcNow;

        foreach (var alert in activeAlerts)
        {
            if (!alert.PortfolioId.HasValue) continue;

            portfolioValues.TryGetValue(alert.PortfolioId.Value, out var currentValue);

            bool triggered = false;
            string msg = string.Empty;

            if (alert.AlertType == "PORTFOLIO_VALUE_ABOVE" && currentValue >= alert.ThresholdValue)
            {
                triggered = true;
                msg = $"Portfolio '{alert.Portfolio?.PortfolioName ?? "Portfolio"}' reached ৳{currentValue:N2}, exceeding your threshold of ৳{alert.ThresholdValue:N2}.";
            }
            else if (alert.AlertType == "PORTFOLIO_VALUE_BELOW" && currentValue <= alert.ThresholdValue)
            {
                triggered = true;
                msg = $"Portfolio '{alert.Portfolio?.PortfolioName ?? "Portfolio"}' dropped to ৳{currentValue:N2}, falling below your threshold of ৳{alert.ThresholdValue:N2}.";
            }

            if (triggered)
            {
                alert.TriggeredAt = now;
                alert.IsActive = false; // Prevent repeated evaluation until user reactivates
                alert.Message = msg;
                triggeredCount++;

                _logger.LogInformation("Portfolio Alert {AlertId} Triggered: {Message}", alert.AlertId, msg);

                if (_notificationService != null)
                {
                    await _notificationService.CreateNotificationFromAlertAsync(
                        alert,
                        title: $"Portfolio Alert: {alert.Portfolio?.PortfolioName ?? "Portfolio"}",
                        message: msg,
                        cancellationToken);
                }
            }
        }

        if (triggeredCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return triggeredCount;
    }

    public async Task<int> EvaluateAllAlertsAsync(CancellationToken cancellationToken = default)
    {
        int p = await EvaluatePriceAlertsAsync(cancellationToken);
        int f = await EvaluatePortfolioAlertsAsync(null, cancellationToken);
        return p + f;
    }

    private async Task EvaluateSingleAlertInternalAsync(Alert alert, CancellationToken cancellationToken)
    {
        if (alert.AlertType == "PRICE_ABOVE" || alert.AlertType == "PRICE_BELOW")
        {
            var company = alert.Company ?? await _context.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyId == alert.CompanyId, cancellationToken);

            if (company != null)
            {
                if (alert.AlertType == "PRICE_ABOVE" && company.CurrentPrice >= alert.ThresholdValue)
                {
                    alert.TriggeredAt = DateTime.UtcNow;
                    alert.IsActive = false;
                    alert.Message = $"{company.TickerSymbol} reached ৳{company.CurrentPrice:N2}, exceeding your target threshold of ৳{alert.ThresholdValue:N2}.";
                    await _context.SaveChangesAsync(cancellationToken);

                    if (_notificationService != null)
                    {
                        await _notificationService.CreateNotificationFromAlertAsync(
                            alert,
                            title: $"Price Alert: {company.TickerSymbol}",
                            message: alert.Message,
                            cancellationToken);
                    }
                }
                else if (alert.AlertType == "PRICE_BELOW" && company.CurrentPrice <= alert.ThresholdValue)
                {
                    alert.TriggeredAt = DateTime.UtcNow;
                    alert.IsActive = false;
                    alert.Message = $"{company.TickerSymbol} fell to ৳{company.CurrentPrice:N2}, dropping below your threshold of ৳{alert.ThresholdValue:N2}.";
                    await _context.SaveChangesAsync(cancellationToken);

                    if (_notificationService != null)
                    {
                        await _notificationService.CreateNotificationFromAlertAsync(
                            alert,
                            title: $"Price Alert: {company.TickerSymbol}",
                            message: alert.Message,
                            cancellationToken);
                    }
                }
            }
        }
        else if (alert.AlertType == "PORTFOLIO_VALUE_ABOVE" || alert.AlertType == "PORTFOLIO_VALUE_BELOW")
        {
            if (alert.PortfolioId.HasValue)
            {
                var pMap = await GetPortfolioMarketValuesAsync(new List<int> { alert.PortfolioId.Value }, cancellationToken);
                var val = pMap.TryGetValue(alert.PortfolioId.Value, out var pv) ? pv : 0m;

                var portfolio = alert.Portfolio ?? await _context.Portfolios
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PortfolioId == alert.PortfolioId.Value, cancellationToken);

                if (alert.AlertType == "PORTFOLIO_VALUE_ABOVE" && val >= alert.ThresholdValue)
                {
                    alert.TriggeredAt = DateTime.UtcNow;
                    alert.IsActive = false;
                    alert.Message = $"Portfolio '{portfolio?.PortfolioName ?? "Portfolio"}' reached ৳{val:N2}, exceeding your threshold of ৳{alert.ThresholdValue:N2}.";
                    await _context.SaveChangesAsync(cancellationToken);

                    if (_notificationService != null)
                    {
                        await _notificationService.CreateNotificationFromAlertAsync(
                            alert,
                            title: $"Portfolio Alert: {portfolio?.PortfolioName ?? "Portfolio"}",
                            message: alert.Message,
                            cancellationToken);
                    }
                }
                else if (alert.AlertType == "PORTFOLIO_VALUE_BELOW" && val <= alert.ThresholdValue)
                {
                    alert.TriggeredAt = DateTime.UtcNow;
                    alert.IsActive = false;
                    alert.Message = $"Portfolio '{portfolio?.PortfolioName ?? "Portfolio"}' dropped to ৳{val:N2}, falling below your threshold of ৳{alert.ThresholdValue:N2}.";
                    await _context.SaveChangesAsync(cancellationToken);

                    if (_notificationService != null)
                    {
                        await _notificationService.CreateNotificationFromAlertAsync(
                            alert,
                            title: $"Portfolio Alert: {portfolio?.PortfolioName ?? "Portfolio"}",
                            message: alert.Message,
                            cancellationToken);
                    }
                }
            }
        }
    }

    private async Task<Dictionary<int, decimal>> GetPortfolioMarketValuesAsync(
        List<int> portfolioIds,
        CancellationToken cancellationToken)
    {
        if (portfolioIds == null || !portfolioIds.Any())
        {
            return new Dictionary<int, decimal>();
        }

        var viewHoldings = await _context.PortfolioHoldings
            .AsNoTracking()
            .Where(h => portfolioIds.Contains(h.PortfolioId))
            .ToListAsync(cancellationToken);

        if (viewHoldings.Any())
        {
            return viewHoldings
                .GroupBy(h => h.PortfolioId)
                .ToDictionary(g => g.Key, g => g.Sum(h => h.CurrentMarketValue));
        }

        // Fallback for InMemory tests or when view is empty: calculate directly from transactions
        var txs = await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Company)
            .Where(t => portfolioIds.Contains(t.PortfolioId) && t.Company != null)
            .ToListAsync(cancellationToken);

        var portfolioValues = portfolioIds.ToDictionary(id => id, _ => 0m);
        var grouped = txs.GroupBy(t => new { t.PortfolioId, t.CompanyId });

        foreach (var g in grouped)
        {
            var company = g.First().Company;
            if (company == null) continue;

            var buyQty = g.Where(t => t.TransactionType == "BUY").Sum(t => t.Quantity);
            var sellQty = g.Where(t => t.TransactionType == "SELL").Sum(t => t.Quantity);
            var netQty = buyQty - sellQty;

            if (netQty > 0)
            {
                var marketVal = netQty * company.CurrentPrice;
                portfolioValues[g.Key.PortfolioId] += marketVal;
            }
        }

        return portfolioValues;
    }

    private static AlertDto MapToDto(Alert a, Dictionary<int, decimal>? portfolioValues = null)
    {
        decimal? portVal = null;
        if (a.PortfolioId.HasValue && portfolioValues != null && portfolioValues.TryGetValue(a.PortfolioId.Value, out var val))
        {
            portVal = val;
        }

        return new AlertDto
        {
            AlertId = a.AlertId,
            UserId = a.UserId,
            CompanyId = a.CompanyId,
            TickerSymbol = a.Company?.TickerSymbol,
            CompanyName = a.Company?.CompanyName,
            CurrentPrice = a.Company?.CurrentPrice,
            PortfolioId = a.PortfolioId,
            PortfolioName = a.Portfolio?.PortfolioName,
            CurrentPortfolioValue = portVal,
            AlertType = a.AlertType,
            ThresholdValue = a.ThresholdValue,
            IsActive = a.IsActive,
            CreatedAt = a.CreatedAt,
            TriggeredAt = a.TriggeredAt,
            Message = a.Message
        };
    }
}
