namespace ShareSync.Domain.Entities;

/// <summary>
/// DSE: Company Catalog (LF-31 Universe Expansion)
/// DATABASE: Table — companies
/// 
/// PURPOSE:
/// Represents a tradeable financial instrument or listed corporate entity on the
/// Dhaka Stock Exchange (or tracked asset universe). Acts as the central anchor for
/// quotes, historical OHLCV data, sector classification, dividends, and user orders.
/// 
/// RELATIONAL MAPPING:
/// - Primary Key: CompanyId (AUTO_INCREMENT in MySQL)
/// - Unique Index: TickerSymbol (uq_companies_ticker)
/// - Foreign Key: SectorId -> sectors(sector_id) ON DELETE RESTRICT
/// - Child Relations: transactions, watchlist_items, dividends, company_price_history
/// </summary>
public class Company
{
    /// <summary>
    /// DATABASE: Primary key identifier for the company/instrument.
    /// </summary>
    public int CompanyId { get; set; }

    /// <summary>
    /// Registered formal legal or corporate name (e.g. "Grameenphone Ltd.").
    /// </summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>
    /// DSE: Ticker Symbol
    /// DATA QUALITY: Ticker Normalization
    /// Canonical trading symbol on DSE (e.g., "GP", "SQUAREPHARMA", "BATBC").
    /// </summary>
    public string TickerSymbol { get; set; } = string.Empty;

    /// <summary>
    /// DATABASE: Foreign key referencing the sector industry classification.
    /// </summary>
    public int SectorId { get; set; }

    /// <summary>
    /// DSE: Live Price Retrieval
    /// Most recent mark-to-market closing or real-time trading quote in instrument currency.
    /// </summary>
    public decimal CurrentPrice { get; set; }

    /// <summary>
    /// Aggregate market capitalization in base currency (optional).
    /// </summary>
    public decimal? MarketCap { get; set; }

    /// <summary>
    /// FEATURE: Multi-Asset (LF-29)
    /// MULTI ASSET: Asset Types
    /// Classification of the instrument:
    /// - "EQUITY": Standard corporate common equity shares
    /// - "MUTUAL_FUND": Closed-end or open-end mutual fund units
    /// - "ETF": Exchange-traded fund baskets
    /// - "BOND": Fixed-income debt securities
    /// - "CASH": Cash or liquidity reserve accounts
    /// </summary>
    public string AssetType { get; set; } = "EQUITY";

    /// <summary>
    /// FEATURE: Multi-Currency (LF-29)
    /// CURRENCY: Instrument Currency
    /// The denomination currency of this security (e.g., "BDT", "USD").
    /// </summary>
    public string Currency { get; set; } = "BDT";

    /// <summary>
    /// Soft-delete flag; true if active for trading/analytics, false if delisted.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Creation timestamp of the instrument record.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to parent sector.
    /// </summary>
    public Sector? Sector { get; set; }

    /// <summary>
    /// Navigation property to transactions executed on this security.
    /// </summary>
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    /// <summary>
    /// Navigation property to user watchlist associations.
    /// </summary>
    public ICollection<WatchlistItem> WatchlistItems { get; set; } = new List<WatchlistItem>();

    /// <summary>
    /// Navigation property to corporate dividend declarations.
    /// </summary>
    public ICollection<Dividend> Dividends { get; set; } = new List<Dividend>();

    /// <summary>
    /// DSE: Historical Data
    /// Navigation property to chronological OHLCV historical quotes.
    /// </summary>
    public ICollection<CompanyPriceHistory> PriceHistories { get; set; } = new List<CompanyPriceHistory>();
}

