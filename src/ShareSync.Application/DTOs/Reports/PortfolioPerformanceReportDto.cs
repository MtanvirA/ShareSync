namespace ShareSync.Application.DTOs.Reports;

public class PerformanceSnapshotItemDto
{
    public int SnapshotId { get; set; }
    public DateTime SnapshotDate { get; set; }
    public decimal PortfolioValue { get; set; }
    public decimal? ChangeFromPrevious { get; set; }
    public decimal? PercentageChange { get; set; }
}

public class PortfolioPerformanceReportDto
{
    public int? PortfolioId { get; set; }
    public string PortfolioName { get; set; } = string.Empty;
    public decimal StartingValue { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal NetChange { get; set; }
    public decimal NetChangePercentage { get; set; }
    public List<string> ChartLabels { get; set; } = new();
    public List<decimal> ChartValues { get; set; } = new();
    public List<PerformanceSnapshotItemDto> Snapshots { get; set; } = new();
}
