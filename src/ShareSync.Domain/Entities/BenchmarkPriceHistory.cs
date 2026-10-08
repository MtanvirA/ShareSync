namespace ShareSync.Domain.Entities;

public class BenchmarkPriceHistory
{
    public int BenchmarkPriceHistoryId { get; set; }
    public string BenchmarkCode { get; set; } = string.Empty;
    public string BenchmarkName { get; set; } = string.Empty;
    public DateTime TradingDate { get; set; }
    public decimal CloseValue { get; set; }
    public decimal? OpenValue { get; set; }
    public decimal? HighValue { get; set; }
    public decimal? LowValue { get; set; }
    public long? Volume { get; set; }
    public string Source { get; set; } = "Dhaka Stock Exchange";
    public string? SourceDataset { get; set; }
    public string? SourceDoi { get; set; }
    public string? ImportBatchId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
