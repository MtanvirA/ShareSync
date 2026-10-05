namespace ShareSync.Domain.Entities;

public class Notification
{
    public int NotificationId { get; set; }
    public int UserId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }

    public AppUser? User { get; set; }
}
