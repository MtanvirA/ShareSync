namespace ShareSync.Application.DTOs.Benchmarks;

public class BenchmarkComparisonRequestDto
{
    public int? PortfolioId { get; set; }
    public string BenchmarkCode { get; set; } = "DSEX";
    public string Period { get; set; } = "ALL";
}
