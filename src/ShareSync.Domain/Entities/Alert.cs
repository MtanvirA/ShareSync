namespace ShareSync.Domain.Entities;

/// <summary>
/// FEATURE: Price & Portfolio Alerts
/// DATABASE: Table — alerts
/// 
/// PURPOSE:
/// Stores threshold alerts configured by investors for market prices and portfolio milestones.
/// Evaluated during market price synchronizations or order executions to fire in-app notifications.
/// 
/// RELATIONAL MAPPING:
/// - Primary Key: AlertId (AUTO_INCREMENT in MySQL)
/// - Foreign Key: UserId -> app_users(user_id) ON DELETE CASCADE
/// - Foreign Key: CompanyId -> companies(company_id) ON DELETE SET NULL
/// - Foreign Key: PortfolioId -> portfolios(portfolio_id) ON DELETE SET NULL
/// - Check Constraint: alert_type IN ('PRICE_ABOVE', 'PRICE_BELOW', 'PORTFOLIO_VALUE')
/// </summary>
public class Alert
{
    /// <summary>
    /// DATABASE: Primary key identifier.
    /// </summary>
    public int AlertId { get; set; }

    /// <summary>
    /// Foreign key referencing the owning user.
    /// SECURITY: Ownership Isolation guarantees alert triggers are dispatched strictly to the creator.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Foreign key referencing monitored company (for price alerts).
    /// </summary>
    public int? CompanyId { get; set; }

    /// <summary>
    /// Foreign key referencing monitored portfolio (for portfolio valuation alerts).
    /// </summary>
    public int? PortfolioId { get; set; }

    /// <summary>
    /// Classification: 'PRICE_ABOVE', 'PRICE_BELOW', or 'PORTFOLIO_VALUE'.
    /// </summary>
    public string AlertType { get; set; } = string.Empty;

    /// <summary>
    /// Trigger target threshold value.
    /// </summary>
    public decimal ThresholdValue { get; set; }

    /// <summary>
    /// Active state flag; false if disabled or already triggered one-shot.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when condition was met and alert fired.
    /// </summary>
    public DateTime? TriggeredAt { get; set; }

    /// <summary>
    /// Formatted notification message generated when alert triggered.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Navigation property to parent user.
    /// </summary>
    public AppUser? User { get; set; }

    /// <summary>
    /// Navigation property to monitored company.
    /// </summary>
    public Company? Company { get; set; }

    /// <summary>
    /// Navigation property to monitored portfolio.
    /// </summary>
    public Portfolio? Portfolio { get; set; }
}

