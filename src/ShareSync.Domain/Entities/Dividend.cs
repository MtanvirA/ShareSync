namespace ShareSync.Domain.Entities;

/// <summary>
/// FEATURE: Dividend Income Tracking
/// DATABASE: Table — dividends
/// 
/// PURPOSE:
/// Represents corporate cash dividend announcements and disbursements for listed securities.
/// Used to compute dividend income reports, historical dividend yield, and cash receipts.
/// 
/// RELATIONAL MAPPING:
/// - Primary Key: DividendId (AUTO_INCREMENT in MySQL)
/// - Foreign Key: CompanyId -> companies(company_id) ON DELETE CASCADE
/// - Check Constraint: dividend_per_share >= 0
/// </summary>
public class Dividend
{
    /// <summary>
    /// DATABASE: Primary key identifier.
    /// </summary>
    public int DividendId { get; set; }

    /// <summary>
    /// DATABASE: Foreign key referencing the distributing company.
    /// </summary>
    public int CompanyId { get; set; }

    /// <summary>
    /// CALCULATION: Dividend Yield
    /// Cash distribution amount per held share unit (e.g. 5.50 BDT).
    /// Gross Payout = HeldQuantityAtRecordDate * DividendPerShare.
    /// </summary>
    public decimal DividendPerShare { get; set; }

    /// <summary>
    /// Formal corporate board declaration date.
    /// </summary>
    public DateTime DeclarationDate { get; set; }

    /// <summary>
    /// Settlement/payout date when cash is disbursed to shareholders.
    /// </summary>
    public DateTime PaymentDate { get; set; }

    /// <summary>
    /// Navigation property to distributing company.
    /// </summary>
    public Company? Company { get; set; }
}

