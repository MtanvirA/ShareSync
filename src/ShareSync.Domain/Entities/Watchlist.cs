namespace ShareSync.Domain.Entities;

/// <summary>
/// FEATURE: Watchlists
/// DATABASE: Table — watchlists
/// 
/// PURPOSE:
/// Represents a user-curated watchlist of securities for price monitoring and target alerts.
/// Supports 1:N watchlists per user, allowing categorization (e.g. "Dividend Stars", "Tech Breakouts").
/// 
/// RELATIONAL MAPPING:
/// - Primary Key: WatchlistId (AUTO_INCREMENT in MySQL)
/// - Foreign Key: UserId -> app_users(user_id) ON DELETE CASCADE
/// - Child Relation: 1:N with watchlist_items
/// </summary>
public class Watchlist
{
    /// <summary>
    /// DATABASE: Primary key identifier.
    /// </summary>
    public int WatchlistId { get; set; }

    /// <summary>
    /// Foreign key referencing the owning user.
    /// SECURITY: Ownership Isolation guarantees users only see their own watchlists.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// User-assigned watchlist title.
    /// </summary>
    public string WatchlistName { get; set; } = string.Empty;

    /// <summary>
    /// Optional description or notes for the watchlist.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to owning user.
    /// </summary>
    public AppUser? User { get; set; }

    /// <summary>
    /// Navigation property to constituent monitored items.
    /// </summary>
    public ICollection<WatchlistItem> Items { get; set; } = new List<WatchlistItem>();
}

