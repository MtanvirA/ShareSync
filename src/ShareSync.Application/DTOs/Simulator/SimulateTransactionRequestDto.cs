namespace ShareSync.Application.DTOs.Simulator;

public class SimulateTransactionRequestDto
{
    public int PortfolioId { get; set; }
    public int CompanyId { get; set; }
    public string TransactionType { get; set; } = "BUY";
    public decimal Quantity { get; set; }
    public decimal HypotheticalPrice { get; set; }
}
