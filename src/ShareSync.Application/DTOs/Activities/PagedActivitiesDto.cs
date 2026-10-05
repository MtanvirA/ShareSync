namespace ShareSync.Application.DTOs.Activities;

public class PagedActivitiesDto
{
    public List<UserActivityDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}
