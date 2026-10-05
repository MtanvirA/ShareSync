namespace ShareSync.Application.DTOs.Activities;

public class UserActivityFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? EventType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
