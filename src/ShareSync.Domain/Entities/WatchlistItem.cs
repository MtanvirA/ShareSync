namespace ShareSync.Domain.Entities;

/// <summary>
/// FEATURE: Watchlist Target Prices
/// DATABASE: Table — watchlist_items
/// DATABASE: Composite Primary Key
/// 
/// PURPOSE:
/// Bridge table representing the M:N relationship between watchlists and companies.
/// Stores optional target purchase prices to track upside/downside gap from current market price.
/// 
/// RELATIONAL MAPPING:
/// - Composite Primary Key: (WatchlistId, CompanyId) via pk_watchlist_items
/// - Foreign Key: WatchlistId -> watchlists(watchlist_id) ON DELETE CASCADE
/// - Foreign Key: CompanyId -> companies(company_id) ON DELETE CASCADE
/// </summary>
public class WatchlistItem
{
    /// <summary>
    /// DATABASE: Foreign key and composite PK component referencing parent watchlist.
    /// </summary>
    public int WatchlistId { get; set; }

    /// <summary>
    /// DATABASE: Foreign key and composite PK component referencing monitored security.
    /// </summary>
    public int CompanyId { get; set; }

    /// <summary>
    /// FEATURE: Target Price Tracking
    /// Desired entry price specified by the investor.
    /// UI calculates: Gap% = ((CurrentPrice - TargetPrice) / TargetPrice) * 100.
    /// </summary>
    public decimal? TargetPrice { get; set; }

    /// <summary>
    /// Timestamp when item was added to the watchlist.
    /// </summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to parent watchlist.
    /// </summary>
    public Watchlist? Watchlist { get; set; }

    /// <summary>
    /// Navigation property to monitored security.
    /// </summary>
    public Company? Company { get; set; }
}

