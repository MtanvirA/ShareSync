namespace ShareSync.Domain.Entities;

public class Transaction
{
    public int TransactionId { get; set; }
    public int PortfolioId { get; set; }
    public int CompanyId { get; set; }
    public string TransactionType { get; set; } = "BUY"; // "BUY" or "SELL"
    public decimal Quantity { get; set; }
    public decimal PricePerShare { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    public Portfolio? Portfolio { get; set; }
    public Company? Company { get; set; }
    public ICollection<TransactionAudit> Audits { get; set; } = new List<TransactionAudit>();
}
