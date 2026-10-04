namespace ShareSync.Domain.Entities;

public class Sector
{
    public int SectorId { get; set; }
    public string SectorName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<Company> Companies { get; set; } = new List<Company>();
}
