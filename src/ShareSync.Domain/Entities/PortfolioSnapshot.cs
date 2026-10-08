namespace ShareSync.Domain.Entities;

/// <summary>
/// FEATURE: Valuation History
/// DATABASE: Table — portfolio_snapshots
/// DATABASE: Stored Procedure — sp_generate_portfolio_snapshot
/// 
/// PURPOSE:
/// Represents a historical end-of-day mark-to-market aggregate portfolio valuation.
/// Generated periodically or via stored procedure to plot historical portfolio growth charts,
/// calculate time-weighted returns, and evaluate benchmark outperformance.
/// 
/// RELATIONAL MAPPING:
/// - Primary Key: SnapshotId (AUTO_INCREMENT in MySQL)
/// - Unique Index: (PortfolioId, SnapshotDate) via uq_snapshots_portfolio_date
/// - Foreign Key: PortfolioId -> portfolios(portfolio_id) ON DELETE CASCADE
/// </summary>
public class PortfolioSnapshot
{
    /// <summary>
    /// DATABASE: Primary key identifier for the snapshot.
    /// </summary>
    public int SnapshotId { get; set; }

    /// <summary>
    /// DATABASE: Foreign key referencing the valued portfolio.
    /// </summary>
    public int PortfolioId { get; set; }

    /// <summary>
    /// Valuation calendar date (YYYY-MM-DD).
    /// </summary>
    public DateTime SnapshotDate { get; set; }

    /// <summary>
    /// CALCULATION: Market Value
    /// Total aggregate portfolio value in base currency on this date:
    /// TotalValue = SUM(HoldingQuantity * ClosingPrice) + CashBalance.
    /// </summary>
    public decimal TotalValue { get; set; }

    /// <summary>
    /// Navigation property to parent portfolio.
    /// </summary>
    public Portfolio? Portfolio { get; set; }
}

