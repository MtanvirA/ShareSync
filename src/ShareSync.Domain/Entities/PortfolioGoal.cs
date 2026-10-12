namespace ShareSync.Domain.Entities;

/// <summary>
/// FEATURE: Portfolio Goals
/// DATABASE: Table — portfolio_goals
/// 
/// PURPOSE:
/// Represents a structured wealth-building objective assigned to a specific portfolio
/// (e.g., "Target Portfolio Value", "Retirement Corpus", "Annual Dividend Target").
/// Evaluated dynamically against active portfolio valuation to report percent progress.
/// 
/// RELATIONAL MAPPING:
/// - Primary Key: GoalId (AUTO_INCREMENT in MySQL)
/// - Foreign Key: UserId -> app_users(user_id) ON DELETE CASCADE
/// - Foreign Key: PortfolioId -> portfolios(portfolio_id) ON DELETE CASCADE
/// - Check Constraint: target_value > 0
/// </summary>
public class PortfolioGoal
{
    /// <summary>
    /// DATABASE: Primary key identifier.
    /// </summary>
    public int GoalId { get; set; }

    /// <summary>
    /// Foreign key referencing the owning user.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Foreign key referencing the targeted portfolio.
    /// </summary>
    public int PortfolioId { get; set; }

    /// <summary>
    /// Classification of the target (e.g. "TARGET_VALUE", "RETIREMENT", "DIVIDEND_INCOME").
    /// </summary>
    public string GoalType { get; set; } = string.Empty;

    /// <summary>
    /// CALCULATION: Goal Progress
    /// Formula: Progress% = (CurrentPortfolioValue / TargetValue) * 100
    /// Target financial valuation threshold in portfolio base currency.
    /// </summary>
    public decimal TargetValue { get; set; }

    /// <summary>
    /// Target achievement completion date (optional deadline).
    /// </summary>
    public DateTime? TargetDate { get; set; }

    /// <summary>
    /// Display title for user interface.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed notes or strategy for reaching the objective.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Goal status; false if achieved or archived.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property to parent user.
    /// </summary>
    public AppUser? User { get; set; }

    /// <summary>
    /// Navigation property to targeted portfolio.
    /// </summary>
    public Portfolio? Portfolio { get; set; }
}

