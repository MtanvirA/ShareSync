namespace ShareSync.Domain.Entities;

public class InflationRate
{
    public int RateId { get; set; }
    public string CountryCode { get; set; } = "BD";
    public int Year { get; set; }
    public decimal Rate { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
