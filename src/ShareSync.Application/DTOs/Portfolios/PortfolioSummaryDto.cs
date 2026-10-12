namespace ShareSync.Application.DTOs.Portfolios;

public class PortfolioSummaryDto
{
    public int PortfolioId { get; set; }
    public string PortfolioName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Currency { get; set; } = "BDT";
    public DateTime CreatedAt { get; set; }
    public decimal TotalValue { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal UnrealizedProfitLoss { get; set; }
    public decimal UnrealizedProfitLossPercentage { get; set; }
    public decimal RealizedProfitLoss { get; set; }
    public decimal TotalProfitLoss => Math.Round(UnrealizedProfitLoss + RealizedProfitLoss, 2);
    public decimal TotalFees { get; set; }
    public decimal NetCashFlow { get; set; }
    public decimal? InflationRate { get; set; }
    public decimal? RealReturnPercentage { get; set; }
    public string? InflationStatus { get; set; }
    public string? InflationPeriod { get; set; }
    public string? LatestCpiMonth { get; set; }
    public int HoldingsCount { get; set; }
    public int TransactionsCount { get; set; }
}
