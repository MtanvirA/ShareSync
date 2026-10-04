namespace ShareSync.Application.DTOs.Transactions;

public class TransactionFilterDto
{
    public int? PortfolioId { get; set; }
    public int? CompanyId { get; set; }
    public string? TransactionType { get; set; } // "BUY", "SELL"
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
