namespace ShareSync.Domain.Entities;

/// <summary>
/// FEATURE: Transaction Management
/// DATABASE: Table — transactions
/// 
/// PURPOSE:
/// Represents an immutable chronological financial trade order (BUY or SELL) executed
/// within an investor's portfolio. Serves as the primary atomic ledger entry for all
/// downstream calculations including holdings, weighted average cost, realized P/L,
/// fees, and net cash flow.
/// 
/// RELATIONAL MAPPING:
/// - Primary Key: TransactionId (AUTO_INCREMENT in MySQL)
/// - Foreign Key: PortfolioId -> portfolios(portfolio_id) ON DELETE RESTRICT
/// - Foreign Key: CompanyId -> companies(company_id) ON DELETE RESTRICT
/// - Trigger Relationship: Audited via trg_transactions_after_* into transaction_audit
/// </summary>
public class Transaction
{
    /// <summary>
    /// DATABASE: Primary key identifier for the transaction record.
    /// </summary>
    public int TransactionId { get; set; }

    /// <summary>
    /// DATABASE: Foreign key referencing the parent portfolio.
    /// SECURITY: Ownership Isolation ensures users can only transact within their own portfolios.
    /// </summary>
    public int PortfolioId { get; set; }

    /// <summary>
    /// DATABASE: Foreign key referencing the traded financial asset/security in companies table.
    /// MULTI ASSET: Supports Equities, Mutual Funds, ETFs, and Cash reserves.
    /// </summary>
    public int CompanyId { get; set; }

    /// <summary>
    /// TRANSACTION: Order direction ("BUY" or "SELL").
    /// VALIDATION: CHECK constraint transaction_type IN ('BUY', 'SELL').
    /// BUSINESS RULE: BUY increases net position; SELL reduces position and triggers Realized P/L calculation.
    /// </summary>
    public string TransactionType { get; set; } = "BUY"; // "BUY" or "SELL"

    /// <summary>
    /// TRANSACTION: Traded quantity/units of the asset.
    /// VALIDATION: CHECK constraint quantity > 0.
    /// BUSINESS RULE: Oversell Prevention checks that SELL Quantity <= Available Holding.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// TRANSACTION: Execution price per unit in the asset's trading currency.
    /// VALIDATION: CHECK constraint price_per_share >= 0.
    /// </summary>
    public decimal PricePerShare { get; set; }

    /// <summary>
    /// FEATURE: Transaction Fees (LF-27)
    /// CALCULATION: Transaction Fee
    /// Brokerage, exchange clearing fees, or transaction costs.
    /// - BUY: Net Outflow = (Quantity * Price) + Fee
    /// - SELL: Net Inflow = (Quantity * Price) - Fee
    /// </summary>
    public decimal Fee { get; set; } = 0.00m;

    /// <summary>
    /// TRANSACTION: UTC timestamp of trade execution.
    /// Used for chronological lot matching, daily valuation snapshots, and tax year tracking.
    /// </summary>
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to parent portfolio.
    /// </summary>
    public Portfolio? Portfolio { get; set; }

    /// <summary>
    /// Navigation property to referenced security/company.
    /// </summary>
    public Company? Company { get; set; }

    /// <summary>
    /// AUDIT: Collection of immutable audit records captured by database triggers.
    /// </summary>
    public ICollection<TransactionAudit> Audits { get; set; } = new List<TransactionAudit>();
}

