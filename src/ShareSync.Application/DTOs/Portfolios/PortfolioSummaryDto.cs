namespace ShareSync.Application.DTOs.Portfolios;

public class PortfolioSummaryDto
{
    public int PortfolioId { get; set; }
    public string PortfolioName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal TotalValue { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal UnrealizedProfitLoss { get; set; }
    public decimal UnrealizedProfitLossPercentage { get; set; }
    public decimal RealizedProfitLoss { get; set; }
    public decimal TotalProfitLoss => Math.Round(UnrealizedProfitLoss + RealizedProfitLoss, 2);
    public decimal TotalFees { get; set; }
    public decimal NetCashFlow { get; set; }
    public int HoldingsCount { get; set; }
    public int TransactionsCount { get; set; }
}
