namespace ShareSync.Application.DTOs.Watchlists;

public class WatchlistDetailDto
{
    public int WatchlistId { get; set; }
    public int UserId { get; set; }
    public string WatchlistName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<WatchlistItemDto> Items { get; set; } = new();

    // Summary metrics
    public int TotalWatching { get; set; }
    public int AboveTargetCount { get; set; }
    public int NearTargetCount { get; set; }
    public decimal AverageDailyChange { get; set; }
}
