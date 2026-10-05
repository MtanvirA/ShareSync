namespace ShareSync.Application.DTOs.Notifications;

public class NotificationDto
{
    public int NotificationId { get; set; }
    public int UserId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }
}

public class CreateNotificationDto
{
    public int UserId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }
}

public class NotificationSummaryDto
{
    public int UnreadCount { get; set; }
    public int TotalCount { get; set; }
    public List<NotificationDto> Notifications { get; set; } = new();
}
