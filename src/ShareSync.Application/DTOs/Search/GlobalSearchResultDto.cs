namespace ShareSync.Application.DTOs.Search;

public class GlobalSearchResultDto
{
    public string Query { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public List<SearchResultItemDto> Companies { get; set; } = new();
    public List<SearchResultItemDto> Sectors { get; set; } = new();
    public List<SearchResultItemDto> Portfolios { get; set; } = new();
    public List<SearchResultItemDto> Watchlist { get; set; } = new();
}
