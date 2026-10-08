namespace ShareSync.Domain.Entities;

/// <summary>
/// SECURITY: Authentication
/// DATABASE: Table — app_users
/// 
/// PURPOSE:
/// Represents an authenticated user identity and credentials. Supports role-based access
/// control (RBAC) separating standard investors from system administrators.
/// 
/// RELATIONAL MAPPING:
/// - Primary Key: UserId (AUTO_INCREMENT in MySQL)
/// - Unique Index: Email (uq_users_email)
/// - Check Constraint: role IN ('INVESTOR', 'ADMIN')
/// - Child Relations: 1:N with portfolios, 1:N with watchlists, 1:N with alerts
/// </summary>
public class AppUser
{
    /// <summary>
    /// DATABASE: Primary key identifier for the user.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Investor or administrator full display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// SECURITY: Authentication
    /// Unique login email address. Normalized to lowercase during lookup.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// SECURITY: Password Hashing
    /// Cryptographic hash produced by PBKDF2 with SHA-256 and 100,000 iterations.
    /// Format: "{iterations}.{base64Salt}.{base64Subkey}". Plaintext is NEVER stored.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// SECURITY: Authorization
    /// Role for RBAC authorization:
    /// - "INVESTOR": Regular account access (portfolios, transactions, personal reports)
    /// - "ADMIN": System oversight (user management, market price sync, system diagnostics)
    /// </summary>
    public string Role { get; set; } = "INVESTOR";

    /// <summary>
    /// Account status; false prevents login access.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Registration UTC timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to portfolios owned by this user.
    /// </summary>
    public ICollection<Portfolio> Portfolios { get; set; } = new List<Portfolio>();

    /// <summary>
    /// Navigation property to watchlists created by this user.
    /// </summary>
    public ICollection<Watchlist> Watchlists { get; set; } = new List<Watchlist>();
}

