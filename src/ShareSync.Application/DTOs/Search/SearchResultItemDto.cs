namespace ShareSync.Application.DTOs.Search;

public class SearchResultItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // "Companies", "Sectors", "Portfolios", "Watchlist"
    public string Url { get; set; } = string.Empty;
    public string? Badge { get; set; }
}
