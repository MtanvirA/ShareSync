namespace ShareSync.Application.DTOs.Benchmarks;

public class BenchmarkDto
{
    public string BenchmarkCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public decimal CurrentValue { get; set; }
    public decimal BaseValue { get; set; }
}
