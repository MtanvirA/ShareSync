namespace ShareSync.Domain.Entities;

/// <summary>
/// FEATURE: Multi-Currency (LF-29)
/// CURRENCY: Exchange Rate
/// DATABASE: Table — currency_exchange_rates
/// 
/// PURPOSE:
/// Stores spot and historical foreign exchange rates between pairs (e.g., USD -> BDT).
/// Used by CurrencyService and PortfolioService to convert foreign asset values into
/// portfolio base reporting currency without frontend hardcoding.
/// 
/// RELATIONAL MAPPING:
/// - Primary Key: RateId (AUTO_INCREMENT in MySQL)
/// - Unique Index: (FromCurrency, ToCurrency, EffectiveDate) via uq_curr_exchange_pair_date
/// </summary>
public class CurrencyExchangeRate
{
    /// <summary>
    /// DATABASE: Primary key identifier.
    /// </summary>
    public int RateId { get; set; }

    /// <summary>
    /// CURRENCY: ISO source base currency code (e.g. "USD").
    /// </summary>
    public string FromCurrency { get; set; } = "USD";

    /// <summary>
    /// CURRENCY: ISO target quote currency code (e.g. "BDT").
    /// </summary>
    public string ToCurrency { get; set; } = "BDT";

    /// <summary>
    /// CALCULATION: Currency Conversion
    /// Formula: ConvertedValue = ForeignAmount * ExchangeRate
    /// Spot conversion rate (e.g., 121.50 BDT per 1 USD).
    /// </summary>
    public decimal ExchangeRate { get; set; }

    /// <summary>
    /// Effective calendar date of this foreign exchange rate.
    /// </summary>
    public DateTime EffectiveDate { get; set; }

    /// <summary>
    /// DATA QUALITY: Provenance
    /// Source reference (e.g., "Bangladesh Bank Reference Rate").
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Record insertion timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

