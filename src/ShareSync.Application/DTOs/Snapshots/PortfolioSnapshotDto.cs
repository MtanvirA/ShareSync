namespace ShareSync.Application.DTOs.Snapshots;

public class PortfolioSnapshotDto
{
    public int SnapshotId { get; set; }
    public int PortfolioId { get; set; }
    public string PortfolioName { get; set; } = string.Empty;
    public DateTime SnapshotDate { get; set; }
    public decimal TotalValue { get; set; }
    public decimal? ValueChange { get; set; }
    public decimal? PercentageChange { get; set; }
}
