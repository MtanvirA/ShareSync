namespace ShareSync.Application.DTOs.Portfolios;

public class PortfolioHoldingDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public string AssetType { get; set; } = "EQUITY";
    public string Currency { get; set; } = "BDT";
    public decimal ExchangeRate { get; set; } = 1.0000m;
    public decimal Shares { get; set; }
    public decimal AverageBuyPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal MarketValue { get; set; }
    public decimal LocalMarketValue { get; set; }
    public decimal BaseMarketValue { get; set; }
    public decimal UnrealizedProfitLoss { get; set; }
    public decimal RealizedProfitLoss { get; set; }
    public decimal TotalProfitLoss => Math.Round(UnrealizedProfitLoss + RealizedProfitLoss, 2);
    public decimal ReturnPercentage { get; set; }
    public decimal AllocationPercentage { get; set; }
}
