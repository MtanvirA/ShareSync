namespace ShareSync.Application.DTOs.Benchmarks;

public class BenchmarkComparisonDto
{
    public int? PortfolioId { get; set; }
    public string PortfolioName { get; set; } = "Consolidated Portfolio (All)";
    public string BenchmarkCode { get; set; } = "DSEX";
    public string BenchmarkName { get; set; } = "DSE Broad Index (DSEX)";
    public string BenchmarkSource { get; set; } = "Dhaka Stock Exchange Reference Series";
    public string DataLimitationNotice { get; set; } = "Controlled reference benchmark index for performance attribution and comparison. Not a live market execution feed.";
    public string Period { get; set; } = "ALL";
    public bool HasSufficientData { get; set; } = true;
    public string? Message { get; set; }
    public List<string> AvailablePeriods { get; set; } = new();

    public decimal PortfolioStartingValue { get; set; }
    public decimal PortfolioEndingValue { get; set; }
    public decimal PortfolioReturnPercentage { get; set; }

    public decimal BenchmarkStartingValue { get; set; }
    public decimal BenchmarkEndingValue { get; set; }
    public decimal BenchmarkReturnPercentage { get; set; }

    public decimal OutperformancePercentage { get; set; }
    public bool IsOutperforming { get; set; }

    public List<string> Labels { get; set; } = new();
    public List<decimal> PortfolioReturns { get; set; } = new();
    public List<decimal> BenchmarkReturns { get; set; } = new();
    public List<decimal> PortfolioValues { get; set; } = new();
    public List<decimal> BenchmarkValues { get; set; } = new();
}
