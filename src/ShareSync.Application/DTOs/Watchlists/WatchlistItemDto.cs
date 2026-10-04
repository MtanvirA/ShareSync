namespace ShareSync.Application.DTOs.Watchlists;

public class WatchlistItemDto
{
    public int WatchlistId { get; set; }
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal? TargetPrice { get; set; }
    public decimal? TargetDistancePercentage { get; set; }
    public bool IsTargetReached { get; set; }
    public decimal DailyChange { get; set; }
    public decimal DailyChangePercentage { get; set; }
    public DateTime AddedAt { get; set; }
}
