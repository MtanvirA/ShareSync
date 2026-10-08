namespace ShareSync.Domain.Entities;

/// <summary>
/// DATABASE: Table — sectors
/// REPORT: Sector Allocation
/// 
/// PURPOSE:
/// Represents an industry classification domain (e.g. "Telecommunication", "Pharmaceuticals",
/// "Bank", "Engineering"). Enables portfolio sector diversification reports, exposure donuts,
/// and sector-wide comparative analytics.
/// 
/// RELATIONAL MAPPING:
/// - Primary Key: SectorId (AUTO_INCREMENT in MySQL)
/// - Unique Index: SectorName via uq_sectors_name
/// - Child Relation: 1:N with companies (ON DELETE RESTRICT)
/// </summary>
public class Sector
{
    /// <summary>
    /// DATABASE: Primary key identifier.
    /// </summary>
    public int SectorId { get; set; }

    /// <summary>
    /// Unique sector industry classification title.
    /// </summary>
    public string SectorName { get; set; } = string.Empty;

    /// <summary>
    /// Optional sector description or industry scope definition.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Navigation property to constituent listed companies in this sector.
    /// </summary>
    public ICollection<Company> Companies { get; set; } = new List<Company>();
}

