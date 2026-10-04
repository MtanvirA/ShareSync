namespace ShareSync.Domain.Entities;

public class WatchlistItem
{
    public int WatchlistId { get; set; }
    public int CompanyId { get; set; }
    public decimal? TargetPrice { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public Watchlist? Watchlist { get; set; }
    public Company? Company { get; set; }
}
