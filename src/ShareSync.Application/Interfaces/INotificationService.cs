using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Notifications;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Interfaces;

public interface INotificationService
{
    Task<ApiResponse<NotificationDto>> CreateNotificationAsync(CreateNotificationDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<NotificationDto>>> GetUserNotificationsAsync(int userId, bool? unreadOnly = null, int limit = 50, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<NotificationSummaryDto>> GetNotificationSummaryAsync(int userId, int limit = 20, CancellationToken cancellationToken = default);
    Task<ApiResponse<NotificationDto>> MarkAsReadAsync(int notificationId, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteNotificationAsync(int notificationId, int userId, CancellationToken cancellationToken = default);
    Task<Notification?> CreateNotificationFromAlertAsync(Alert alert, string title, string message, CancellationToken cancellationToken = default);
}
