namespace ShareSync.Domain.Entities;

/// <summary>
/// FEATURE: Inflation-Adjusted Return (LF-28)
/// INFLATION: CPI Data
/// DATABASE: Table — inflation_rates
/// 
/// PURPOSE:
/// Stores annual headline inflation figures published by official statistical agencies
/// (Bangladesh Bureau of Statistics - BBS for "BD", US Bureau of Labor Statistics - BLS for "US").
/// Used by InflationService to compute real (purchasing-power adjusted) portfolio returns.
/// 
/// RELATIONAL MAPPING:
/// - Primary Key: RateId (AUTO_INCREMENT in MySQL)
/// - Unique Index: (CountryCode, Year) via uq_inflation_country_year
/// </summary>
public class InflationRate
{
    /// <summary>
    /// DATABASE: Primary key identifier.
    /// </summary>
    public int RateId { get; set; }

    /// <summary>
    /// INFLATION: ISO 2-letter country code ("BD" for Bangladesh, "US" for United States).
    /// </summary>
    public string CountryCode { get; set; } = "BD";

    /// <summary>
    /// Calendar year of the reported annual inflation rate.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// INFLATION: Annual headline inflation percentage (e.g., 9.73 for 9.73%).
    /// CALCULATION: Real Return uses (1 + NominalReturn) / (1 + CumulativeInflation) - 1.
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// DATA QUALITY: Provenance
    /// Official data source agency (e.g., "Bangladesh Bureau of Statistics (BBS)").
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Record creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

