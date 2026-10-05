namespace ShareSync.Application.DTOs.Analytics;

public class PortfolioAnalyticsDto
{
    // Context
    public int? PortfolioId { get; set; }
    public string PortfolioName { get; set; } = "All Portfolios";

    // 1. Valuation & Financial Return Metrics
    public decimal PortfolioValue { get; set; }
    public decimal InvestedCapital { get; set; }
    public decimal UnrealizedProfitLoss { get; set; }
    public decimal UnrealizedReturnPercentage { get; set; }
    public decimal RealizedProfitLoss { get; set; }
    public decimal DividendIncome { get; set; }
    public decimal DividendYield { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal TotalReturnPercentage { get; set; }

    // 2. Portfolio Concentration & Risk Metrics
    public decimal TopHoldingWeight { get; set; }
    public decimal LargestHoldingWeight => TopHoldingWeight;
    public decimal LargestSectorWeight { get; set; }
    public decimal DiversificationScore { get; set; }
    public decimal Top3Concentration { get; set; }
    public decimal Top5Concentration { get; set; }
    public decimal HerfindahlIndex { get; set; }
    public string ConcentrationStatus { get; set; } = "Diversified";
    public int TotalHoldingsCount { get; set; }
    public int TotalSectorsCount { get; set; }

    // 3. Sector Allocation
    public List<AnalyticsSectorDto> SectorAllocation { get; set; } = new();

    // 4. Company Allocation
    public List<AnalyticsCompanyDto> CompanyAllocation { get; set; } = new();

    // 5. Top Gainers & Losers
    public List<AnalyticsPerformanceItemDto> TopGainers { get; set; } = new();
    public List<AnalyticsPerformanceItemDto> TopLosers { get; set; } = new();

    // 6. Historical Performance Chart
    public AnalyticsPerformanceChartDto HistoricalPerformance { get; set; } = new();

    // 7. Explicit Accounting Formulas (Financial Transparency)
    public Dictionary<string, string> MetricFormulas { get; set; } = new();
}

public class AnalyticsSectorDto
{
    public string SectorName { get; set; } = string.Empty;
    public decimal MarketValue { get; set; }
    public decimal InvestedValue { get; set; }
    public decimal UnrealizedProfitLoss { get; set; }
    public decimal AllocationPercentage { get; set; }
    public int HoldingsCount { get; set; }
}

public class AnalyticsCompanyDto
{
    public int CompanyId { get; set; }
    public string TickerSymbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string SectorName { get; set; } = string.Empty;
    public decimal Shares { get; set; }
    public decimal AverageBuyPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal MarketValue { get; set; }
    public decimal InvestedValue { get; set; }
    public decimal UnrealizedProfitLoss { get; set; }
    public decimal ReturnPercentage { get; set; }
    public decimal AllocationPercentage { get; set; }
}

public class AnalyticsPerformanceItemDto
{
    public int CompanyId { get; set; }
    public string TickerSymbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal AverageBuyPrice { get; set; }
    public decimal UnrealizedProfitLoss { get; set; }
    public decimal ReturnPercentage { get; set; }
    public decimal MarketValue { get; set; }
}

public class AnalyticsPerformanceChartDto
{
    public string Period { get; set; } = "ALL";
    public decimal StartingValue { get; set; }
    public decimal EndingValue { get; set; }
    public decimal NetChange { get; set; }
    public decimal NetChangePercentage { get; set; }
    public List<string> Labels { get; set; } = new();
    public List<decimal> Values { get; set; } = new();
}
