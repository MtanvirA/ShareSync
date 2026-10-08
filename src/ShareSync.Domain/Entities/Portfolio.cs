namespace ShareSync.Domain.Entities;

/// <summary>
/// FEATURE: Multi-Portfolio Management
/// DATABASE: Table — portfolios
/// 
/// PURPOSE:
/// Represents an investor's segregated investment portfolio account. Supports 1:N
/// portfolio ownership per user, allowing an investor to segregate strategies
/// (e.g., "Long-Term Growth", "Dividend Income", "High-Risk Tech").
/// 
/// RELATIONAL MAPPING:
/// - Primary Key: PortfolioId (AUTO_INCREMENT in MySQL)
/// - Foreign Key: UserId -> app_users(user_id) ON DELETE CASCADE
/// - Child Relations: 1:N with transactions, 1:N with portfolio_snapshots, 1:N with portfolio_goals
/// </summary>
public class Portfolio
{
    /// <summary>
    /// DATABASE: Primary key identifier for the portfolio.
    /// </summary>
    public int PortfolioId { get; set; }

    /// <summary>
    /// DATABASE: Foreign key referencing the owning user.
    /// SECURITY: Ownership Isolation guarantees users can only access their own portfolios.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// User-friendly portfolio display name (e.g., "Retirement Corpus").
    /// </summary>
    public string PortfolioName { get; set; } = string.Empty;

    /// <summary>
    /// Optional strategy description or investment mandate notes.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// FEATURE: Multi-Currency (LF-29)
    /// CURRENCY: Base Currency
    /// The reporting base currency for portfolio valuation aggregation (defaults to "BDT").
    /// Foreign asset valuations (e.g., USD instruments) are converted into this currency.
    /// </summary>
    public string Currency { get; set; } = "BDT";

    /// <summary>
    /// Timestamp when portfolio was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to parent user.
    /// </summary>
    public AppUser? User { get; set; }

    /// <summary>
    /// Navigation property to portfolio transactions ledger.
    /// </summary>
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    /// <summary>
    /// Navigation property to mark-to-market daily valuation snapshots.
    /// </summary>
    public ICollection<PortfolioSnapshot> Snapshots { get; set; } = new List<PortfolioSnapshot>();
}

