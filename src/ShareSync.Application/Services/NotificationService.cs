using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Notifications;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IApplicationDbContext context, ILogger<NotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResponse<NotificationDto>> CreateNotificationAsync(
        CreateNotificationDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.UserId <= 0)
        {
            throw new AppException("A valid User ID is required.", 400);
        }

        var title = (request.Title ?? string.Empty).Trim();
        var message = (request.Message ?? string.Empty).Trim();
        var type = (request.NotificationType ?? "SYSTEM").Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new AppException("Notification title cannot be empty.", 400);
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new AppException("Notification message cannot be empty.", 400);
        }

        // Duplicate Prevention: Check if identical notification was created recently
        var duplicateExists = await _context.Notifications.AnyAsync(n =>
            n.UserId == request.UserId &&
            n.NotificationType == type &&
            n.RelatedEntityType == request.RelatedEntityType &&
            n.RelatedEntityId == request.RelatedEntityId &&
            n.Title == title &&
            n.CreatedAt >= DateTime.UtcNow.AddHours(-1),
            cancellationToken);

        if (duplicateExists)
        {
            _logger.LogInformation("Duplicate notification prevented for user {UserId}: {Title}", request.UserId, title);
            var existing = await _context.Notifications
                .Where(n => n.UserId == request.UserId && n.Title == title)
                .OrderByDescending(n => n.CreatedAt)
                .FirstAsync(cancellationToken);

            return ApiResponse<NotificationDto>.Ok(MapToDto(existing), "Existing notification returned (duplicate suppressed).");
        }

        var notification = new Notification
        {
            UserId = request.UserId,
            NotificationType = type,
            Title = title,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            RelatedEntityType = request.RelatedEntityType,
            RelatedEntityId = request.RelatedEntityId
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<NotificationDto>.Ok(MapToDto(notification), "Notification created successfully.");
    }

    public async Task<ApiResponse<List<NotificationDto>>> GetUserNotificationsAsync(
        int userId,
        bool? unreadOnly = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);

        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (unreadOnly.HasValue && unreadOnly.Value)
        {
            query = query.Where(n => !n.IsRead);
        }

        var list = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(safeLimit)
            .Select(n => MapToDto(n))
            .ToListAsync(cancellationToken);

        return ApiResponse<List<NotificationDto>>.Ok(list);
    }

    public async Task<ApiResponse<int>> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default)
    {
        var count = await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

        return ApiResponse<int>.Ok(count);
    }

    public async Task<ApiResponse<NotificationSummaryDto>> GetNotificationSummaryAsync(
        int userId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);

        var unreadCount = await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

        var totalCount = await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId, cancellationToken);

        var recentList = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(safeLimit)
            .Select(n => MapToDto(n))
            .ToListAsync(cancellationToken);

        var summary = new NotificationSummaryDto
        {
            UnreadCount = unreadCount,
            TotalCount = totalCount,
            Notifications = recentList
        };

        return ApiResponse<NotificationSummaryDto>.Ok(summary);
    }

    public async Task<ApiResponse<NotificationDto>> MarkAsReadAsync(
        int notificationId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId, cancellationToken);

        if (notification == null)
        {
            throw new NotFoundException("Notification", notificationId);
        }

        // Ownership enforcement
        if (notification.UserId != userId)
        {
            throw new AppException("You do not have permission to access or modify this notification.", 403);
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return ApiResponse<NotificationDto>.Ok(MapToDto(notification), "Notification marked as read.");
    }

    public async Task<ApiResponse<int>> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default)
    {
        var unreadNotifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        if (!unreadNotifications.Any())
        {
            return ApiResponse<int>.Ok(0, "No unread notifications to mark.");
        }

        foreach (var n in unreadNotifications)
        {
            n.IsRead = true;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<int>.Ok(unreadNotifications.Count, $"Marked {unreadNotifications.Count} notification(s) as read.");
    }

    public async Task<ApiResponse<bool>> DeleteNotificationAsync(
        int notificationId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId, cancellationToken);

        if (notification == null)
        {
            throw new NotFoundException("Notification", notificationId);
        }

        // Ownership enforcement
        if (notification.UserId != userId)
        {
            throw new AppException("You do not have permission to delete this notification.", 403);
        }

        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, "Notification deleted successfully.");
    }

    public async Task<Notification?> CreateNotificationFromAlertAsync(
        Alert alert,
        string title,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (alert == null) return null;

        var type = alert.AlertType.StartsWith("PRICE") ? "PRICE_ALERT" : "PORTFOLIO_ALERT";

        // Duplicate Check: Check if a notification for this alert was already generated in the last 24 hours
        var exists = await _context.Notifications.AnyAsync(n =>
            n.UserId == alert.UserId &&
            n.RelatedEntityType == "ALERT" &&
            n.RelatedEntityId == alert.AlertId &&
            n.CreatedAt >= DateTime.UtcNow.AddHours(-24),
            cancellationToken);

        if (exists)
        {
            _logger.LogInformation("Alert notification already generated for Alert #{AlertId}, skipping duplicate.", alert.AlertId);
            return null;
        }

        var notification = new Notification
        {
            UserId = alert.UserId,
            NotificationType = type,
            Title = title,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            RelatedEntityType = "ALERT",
            RelatedEntityId = alert.AlertId
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Generated Notification #{NotificationId} from Alert #{AlertId} for User #{UserId}",
            notification.NotificationId, alert.AlertId, alert.UserId);

        return notification;
    }

    private static NotificationDto MapToDto(Notification n)
    {
        return new NotificationDto
        {
            NotificationId = n.NotificationId,
            UserId = n.UserId,
            NotificationType = n.NotificationType,
            Title = n.Title,
            Message = n.Message,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt,
            RelatedEntityType = n.RelatedEntityType,
            RelatedEntityId = n.RelatedEntityId
        };
    }
}
