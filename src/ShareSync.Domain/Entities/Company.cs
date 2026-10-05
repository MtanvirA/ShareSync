namespace ShareSync.Domain.Entities;

public class Company
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public int SectorId { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal? MarketCap { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Sector? Sector { get; set; }
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<WatchlistItem> WatchlistItems { get; set; } = new List<WatchlistItem>();
    public ICollection<Dividend> Dividends { get; set; } = new List<Dividend>();
    public ICollection<CompanyPriceHistory> PriceHistories { get; set; } = new List<CompanyPriceHistory>();
}
