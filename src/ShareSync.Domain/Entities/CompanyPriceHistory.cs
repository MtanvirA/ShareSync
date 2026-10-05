namespace ShareSync.Domain.Entities;

public class CompanyPriceHistory
{
    public long PriceHistoryId { get; set; }
    public int CompanyId { get; set; }
    public decimal Price { get; set; }
    public decimal? OpenPrice { get; set; }
    public decimal? HighPrice { get; set; }
    public decimal? LowPrice { get; set; }
    public long? Volume { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Company? Company { get; set; }
}
