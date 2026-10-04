namespace ShareSync.Application.DTOs.Portfolios;

public class PortfolioHoldingDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public decimal Shares { get; set; }
    public decimal AverageBuyPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal MarketValue { get; set; }
    public decimal UnrealizedProfitLoss { get; set; }
    public decimal ReturnPercentage { get; set; }
    public decimal AllocationPercentage { get; set; }
}
