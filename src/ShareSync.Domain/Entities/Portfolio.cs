namespace ShareSync.Domain.Entities;

public class Portfolio
{
    public int PortfolioId { get; set; }
    public int UserId { get; set; }
    public string PortfolioName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AppUser? User { get; set; }
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<PortfolioSnapshot> Snapshots { get; set; } = new List<PortfolioSnapshot>();
}
