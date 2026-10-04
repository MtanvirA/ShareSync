namespace ShareSync.Domain.Entities;

public class Watchlist
{
    public int WatchlistId { get; set; }
    public int UserId { get; set; }
    public string WatchlistName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AppUser? User { get; set; }
    public ICollection<WatchlistItem> Items { get; set; } = new List<WatchlistItem>();
}
