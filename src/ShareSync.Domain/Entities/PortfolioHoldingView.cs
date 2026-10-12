namespace ShareSync.Domain.Entities;

/// <summary>
/// DATABASE: View — vw_portfolio_holdings
/// HOLDINGS: Current Quantity
/// 
/// PURPOSE:
/// Keyless EF Core entity mapped to the MySQL database view `vw_portfolio_holdings`.
/// Computes net share positions, mark-to-market market valuations, volume-weighted average
/// purchase prices, and unrealized profit/loss on demand without storing redundant
/// derived data in tables.
/// 
/// FINANCIAL FORMULAS:
/// - CurrentQuantity = SUM(BUY quantity) - SUM(SELL quantity)
/// - CurrentMarketValue = CurrentQuantity * CurrentPrice
/// - WeightedAverageBuyPrice = SUM(BUY quantity * BUY price) / SUM(BUY quantity)
/// - UnrealizedProfitLoss = (CurrentPrice - WeightedAverageBuyPrice) * CurrentQuantity
/// </summary>
public class PortfolioHoldingView
{
    /// <summary>
    /// DATABASE: ID of the owning portfolio.
    /// </summary>
    public int PortfolioId { get; set; }

    /// <summary>
    /// Portfolio name.
    /// </summary>
    public string PortfolioName { get; set; } = string.Empty;

    /// <summary>
    /// DATABASE: ID of the held security/company.
    /// </summary>
    public int CompanyId { get; set; }

    /// <summary>
    /// Registered company name.
    /// </summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>
    /// Canonical ticker symbol (e.g., "GP", "SQUAREPHARMA").
    /// </summary>
    public string TickerSymbol { get; set; } = string.Empty;

    /// <summary>
    /// HOLDINGS: Current Quantity
    /// Net remaining position balance: SUM(BUY) - SUM(SELL).
    /// Positions with 0 remaining quantity are filtered out of active views.
    /// </summary>
    public decimal CurrentQuantity { get; set; }

    /// <summary>
    /// Latest closing or real-time trading price per unit in trading currency.
    /// </summary>
    public decimal CurrentPrice { get; set; }

    /// <summary>
    /// CALCULATION: Market Value
    /// Formula: CurrentMarketValue = CurrentQuantity * CurrentPrice
    /// Total mark-to-market gross value of the active holding.
    /// </summary>
    public decimal CurrentMarketValue { get; set; }

    /// <summary>
    /// CALCULATION: Weighted Average Cost
    /// Formula: WeightedAverageBuyPrice = SUM(BUY qty * BUY price) / SUM(BUY qty)
    /// Represents the volume-weighted cost basis of the position.
    /// </summary>
    public decimal WeightedAverageBuyPrice { get; set; }

    /// <summary>
    /// CALCULATION: Unrealized P/L
    /// Formula: UnrealizedProfitLoss = (CurrentPrice - WeightedAverageBuyPrice) * CurrentQuantity
    /// Mark-to-market paper profit or loss if the entire position were liquidated at CurrentPrice.
    /// </summary>
    public decimal UnrealizedProfitLoss { get; set; }
}

