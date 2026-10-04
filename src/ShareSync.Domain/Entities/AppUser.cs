namespace ShareSync.Domain.Entities;

public class AppUser
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "INVESTOR";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Portfolio> Portfolios { get; set; } = new List<Portfolio>();
    public ICollection<Watchlist> Watchlists { get; set; } = new List<Watchlist>();
}
