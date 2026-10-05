namespace ShareSync.Application.DTOs.Companies;

public class CompanyPriceHistoryDto
{
    public long PriceHistoryId { get; set; }
    public int CompanyId { get; set; }
    public decimal Price { get; set; }
    public decimal? OpenPrice { get; set; }
    public decimal? HighPrice { get; set; }
    public decimal? LowPrice { get; set; }
    public long? Volume { get; set; }
    public DateTime RecordedAt { get; set; }
}

public class CompanyPriceHistoryResponseDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public string Period { get; set; } = "ALL";
    public int TotalPoints { get; set; }
    public decimal? PeriodHigh { get; set; }
    public decimal? PeriodLow { get; set; }
    public decimal? PeriodChange { get; set; }
    public decimal? PeriodChangePercentage { get; set; }
    public List<CompanyPriceHistoryDto> History { get; set; } = new();
}

public class CompanyPriceHistoryFilterDto
{
    public string? Period { get; set; } // "1D", "1W", "1M", "3M", "6M", "1Y", "ALL"
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? Limit { get; set; } = 200;
}
