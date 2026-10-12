namespace ShareSync.Application.DTOs.Inflation;

public class CpiIndexObservationDto
{
    public int ObservationId { get; set; }
    public string CountryCode { get; set; } = "BD";
    public DateTime ObservationDate { get; set; }
    public string ObservationPeriod => ObservationDate.ToString("yyyy-MM");
    public string MonthName => ObservationDate.ToString("MMM yyyy");
    public decimal CpiIndex { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
}
