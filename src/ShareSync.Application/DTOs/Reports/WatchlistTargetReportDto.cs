namespace ShareSync.Application.DTOs.Reports;

public class WatchlistTargetItemDto
{
    public int WatchlistId { get; set; }
    public string WatchlistName { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal TargetPrice { get; set; }
    public decimal PriceDifference { get; set; } // TargetPrice - CurrentPrice
    public decimal PercentageDifference { get; set; } // ((TargetPrice - CurrentPrice) / CurrentPrice) * 100
    public string Status { get; set; } = string.Empty; // "Above Target", "Near Target", "Below Target"
}

public class WatchlistTargetReportDto
{
    public int TotalItems { get; set; }
    public int AboveTargetCount { get; set; }
    public int NearTargetCount { get; set; }
    public int BelowTargetCount { get; set; }
    public List<WatchlistTargetItemDto> Items { get; set; } = new();
}
