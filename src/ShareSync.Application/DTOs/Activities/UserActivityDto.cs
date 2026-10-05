namespace ShareSync.Application.DTOs.Activities;

public class UserActivityDto
{
    public string ActivityId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }
    public string? Icon { get; set; }
    public string? BadgeClass { get; set; }
}
