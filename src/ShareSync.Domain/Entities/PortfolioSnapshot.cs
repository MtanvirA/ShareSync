namespace ShareSync.Domain.Entities;

public class PortfolioSnapshot
{
    public int SnapshotId { get; set; }
    public int PortfolioId { get; set; }
    public DateTime SnapshotDate { get; set; }
    public decimal TotalValue { get; set; }

    public Portfolio? Portfolio { get; set; }
}
