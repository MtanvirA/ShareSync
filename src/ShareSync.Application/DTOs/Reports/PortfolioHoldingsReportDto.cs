namespace ShareSync.Application.DTOs.Reports;

public class PortfolioHoldingsReportItemDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public int PortfolioId { get; set; }
    public string PortfolioName { get; set; } = string.Empty;
    public decimal CurrentQuantity { get; set; }
    public decimal WeightedAverageBuyPrice { get; set; }
    public decimal WeightedAveragePurchasePrice => WeightedAverageBuyPrice;
    public decimal CurrentMarketPrice { get; set; }
    public decimal InvestedValue { get; set; }
    public decimal CurrentMarketValue { get; set; }
    public decimal UnrealizedProfitLoss { get; set; }
    public decimal ProfitLossPercentage { get; set; }
}

public class PortfolioHoldingsReportDto
{
    public decimal TotalInvested { get; set; }
    public decimal TotalMarketValue { get; set; }
    public decimal TotalUnrealizedProfitLoss { get; set; }
    public decimal TotalProfitLossPercentage { get; set; }
    public int HoldingsCount { get; set; }
    public List<PortfolioHoldingsReportItemDto> Holdings { get; set; } = new();
}
