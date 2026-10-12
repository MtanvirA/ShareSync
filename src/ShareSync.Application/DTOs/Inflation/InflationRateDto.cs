namespace ShareSync.Application.DTOs.Inflation;

public class InflationRateDto
{
    public int RateId { get; set; }
    public string CountryCode { get; set; } = "BD";
    public int Year { get; set; }
    public decimal InflationRate { get; set; }
    public string Source { get; set; } = string.Empty;
}

public class RealReturnCalculationResultDto
{
    public decimal NominalReturnPercentage { get; set; }
    public decimal? InflationRatePercentage { get; set; }
    public decimal? InflationRate => InflationRatePercentage;
    public decimal? RealReturnPercentage { get; set; }
    public decimal? RealReturn => RealReturnPercentage;
    public string CountryCode { get; set; } = "BD";
    public string Period { get; set; } = string.Empty;
    public bool IsDataAvailable { get; set; }
    public string Status { get; set; } = "Calculated";
    public string Methodology { get; set; } = "Fisher-model Real Return = ((1 + Nominal / 100) / (1 + Inflation / 100) - 1) * 100";
    public decimal? StartingCpiIndex { get; set; }
    public decimal? EndingCpiIndex { get; set; }
    public string? ObservationPeriodStart { get; set; }
    public string? ObservationPeriodEnd { get; set; }
    public string? LatestObservationMonth { get; set; }
    public string? Source { get; set; }
    public string? SourceUrl { get; set; }
}

