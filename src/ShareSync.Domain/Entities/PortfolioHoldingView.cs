namespace ShareSync.Domain.Entities;

public class PortfolioHoldingView
{
    public int PortfolioId { get; set; }
    public string PortfolioName { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public decimal CurrentQuantity { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal CurrentMarketValue { get; set; }
    public decimal WeightedAverageBuyPrice { get; set; }
    public decimal UnrealizedProfitLoss { get; set; }
}
