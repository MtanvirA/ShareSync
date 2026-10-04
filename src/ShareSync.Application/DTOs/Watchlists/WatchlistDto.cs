namespace ShareSync.Application.DTOs.Watchlists;

public class WatchlistDto
{
    public int WatchlistId { get; set; }
    public int UserId { get; set; }
    public string WatchlistName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ItemCount { get; set; }
}
