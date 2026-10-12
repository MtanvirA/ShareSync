namespace ShareSync.Domain.Entities;

/// <summary>
/// FEATURE: Inflation-Adjusted Return (LF-28) - Monthly CPI Index Series
/// DATABASE: Table — cpi_index_observations
/// 
/// PURPOSE:
/// Stores official monthly Consumer Price Index (CPI) index levels published by official
/// statistical authorities (Bangladesh Bureau of Statistics - BBS for "BD").
/// Used by InflationService to compute precise date-range cumulative inflation:
/// Cumulative Inflation (%) = ((Ending CPI / Starting CPI) - 1) * 100
/// 
/// RELATIONAL MAPPING:
/// - Primary Key: ObservationId (AUTO_INCREMENT in MySQL)
/// - Unique Index: (CountryCode, ObservationDate) via uq_cpi_observations_country_date
/// - Check Constraint: CpiIndex > 0
/// </summary>
public class CpiIndexObservation
{
    /// <summary>
    /// DATABASE: Primary key identifier.
    /// </summary>
    public int ObservationId { get; set; }

    /// <summary>
    /// INFLATION: ISO 2-letter country code ("BD" for Bangladesh, "US" for United States).
    /// </summary>
    public string CountryCode { get; set; } = "BD";

    /// <summary>
    /// Observation period date (normalized to 1st day of observation calendar month, e.g. 2026-08-01).
    /// </summary>
    public DateTime ObservationDate { get; set; }

    /// <summary>
    /// Official CPI index level (e.g. 151.6366 for Base 2021-22 = 100).
    /// </summary>
    public decimal CpiIndex { get; set; }

    /// <summary>
    /// DATA QUALITY: Provenance
    /// Official publishing agency and series description (e.g. "Bangladesh Bureau of Statistics (BBS) - National General CPI (Base 2021-22=100)").
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// DATA QUALITY: Provenance URL
    /// Official publication portal URL.
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Record creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
