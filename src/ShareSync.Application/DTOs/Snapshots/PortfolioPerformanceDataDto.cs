namespace ShareSync.Application.DTOs.Snapshots;

public class PortfolioPerformanceDataDto
{
    public int PortfolioId { get; set; }
    public string PortfolioName { get; set; } = string.Empty;
    public List<string> Labels { get; set; } = new();
    public List<decimal> Values { get; set; } = new();
    public List<PortfolioSnapshotDto> Snapshots { get; set; } = new();
    public decimal CurrentValue { get; set; }
    public decimal StartingValue { get; set; }
    public decimal OverallChange { get; set; }
    public decimal OverallChangePercentage { get; set; }
}
