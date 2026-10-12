namespace ShareSync.Domain.Entities;

/// <summary>
/// DSE: Historical Data (LF-31 Market Dataset)
/// DATA QUALITY: OHLC Validation
/// DATABASE: Table — company_price_history
/// 
/// PURPOSE:
/// Stores chronological daily OHLCV market observations for listed companies.
/// Powers the Investment Intelligence quantitative engine, retrospective statistics,
/// candlestick charts, moving averages (SMA50, SMA200), and historical volatility.
/// 
/// RELATIONAL MAPPING:
/// - Primary Key: PriceHistoryId (BIGINT AUTO_INCREMENT in MySQL)
/// - Foreign Key: CompanyId -> companies(company_id) ON DELETE CASCADE
/// - Unique Index: (CompanyId, TradingDate) via uq_cph_company_date
/// - Range Index: idx_cph_company_date for fast chronological window retrieval
/// </summary>
public class CompanyPriceHistory
{
    /// <summary>
    /// DATABASE: Primary key identifier.
    /// </summary>
    public long PriceHistoryId { get; set; }

    /// <summary>
    /// Foreign key referencing the parent company.
    /// </summary>
    public int CompanyId { get; set; }

    /// <summary>
    /// Closing execution price of the security on TradingDate.
    /// VALIDATION: CHECK constraint price > 0.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// DATA QUALITY: OHLC
    /// Opening quote of the trading session.
    /// </summary>
    public decimal? OpenPrice { get; set; }

    /// <summary>
    /// DATA QUALITY: OHLC
    /// Intraday session high. Enforces High >= Low in import validation.
    /// </summary>
    public decimal? HighPrice { get; set; }

    /// <summary>
    /// DATA QUALITY: OHLC
    /// Intraday session low. Enforces Low <= High in import validation.
    /// </summary>
    public decimal? LowPrice { get; set; }

    /// <summary>
    /// Aggregate share volume traded on this date.
    /// Non-negative integer. Zero-volume trading days are preserved to prevent survivorship bias.
    /// </summary>
    public long? Volume { get; set; }

    /// <summary>
    /// Legacy recording timestamp.
    /// </summary>
    public DateTime RecordedAt { get; set; }

    /// <summary>
    /// Canonical calendar trading date (YYYY-MM-DD).
    /// </summary>
    public DateTime? TradingDate { get; set; }

    /// <summary>
    /// DATA QUALITY: Provenance
    /// Institutional source of the historical observation (e.g., "Dhaka Stock Exchange").
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Academic repository or dataset identifier (e.g., "Harvard Dataverse DSE Archive").
    /// </summary>
    public string? SourceDataset { get; set; }

    /// <summary>
    /// Permanent Digital Object Identifier citing scientific provenance.
    /// </summary>
    public string? SourceDoi { get; set; }

    /// <summary>
    /// Import execution batch identifier.
    /// </summary>
    public string? ImportBatchId { get; set; }

    /// <summary>
    /// Database insertion timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to referenced company.
    /// </summary>
    public Company? Company { get; set; }
}

