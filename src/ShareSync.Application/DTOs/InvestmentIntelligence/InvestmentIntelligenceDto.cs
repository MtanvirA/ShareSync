using System;
using System.Collections.Generic;

namespace ShareSync.Application.DTOs.InvestmentIntelligence;

public class HistoricalInvestmentProfileDto
{
    public CompanyInfoDto Company { get; set; } = new();
    public AnalysisPeriodDto AnalysisPeriod { get; set; } = new();
    public SummaryDto Summary { get; set; } = new();
    public RiskDto Risk { get; set; } = new();
    public ConsistencyDto Consistency { get; set; } = new();
    public TrendDto Trend { get; set; } = new();
    public ScoreDto Score { get; set; } = new();
    public List<YearlyPerformanceDto> YearlyPerformance { get; set; } = new();
    public List<ChartDataPointDto> ChartData { get; set; } = new();
    public ExplanationsDto Explanations { get; set; } = new();
    public DataQualityDto DataQuality { get; set; } = new();
    public SourceDto Source { get; set; } = new();
    public string Disclaimer { get; set; } = "This system evaluates historical market behavior. It does not guarantee future performance and does not constitute financial advice.";
}

public class CompanyInfoDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TradingCode { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
}

public class AnalysisPeriodDto
{
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public double Years { get; set; }
    public bool HasFullFiveYearHistory { get; set; }
}

public class SummaryDto
{
    public decimal StartingPrice { get; set; }
    public decimal EndingPrice { get; set; }
    public decimal HistoricalReturnPercentage { get; set; }
    public decimal CagrPercentage { get; set; }
}

public class RiskDto
{
    public decimal DailyVolatility { get; set; }
    public decimal AnnualizedVolatilityPercentage { get; set; }
    public decimal MaximumDrawdownPercentage { get; set; }
    public string MaximumDrawdownDate { get; set; } = string.Empty;
}

public class ConsistencyDto
{
    public int PositiveDays { get; set; }
    public int NegativeDays { get; set; }
    public int FlatDays { get; set; }
    public int ValidReturnDays { get; set; }
    public decimal PositiveDayPercentage { get; set; }
    public int PositiveYears { get; set; }
    public int NegativeYears { get; set; }
}

public class TrendDto
{
    public decimal LatestClose { get; set; }
    public decimal? LatestMA50 { get; set; }
    public decimal? LatestMA200 { get; set; }
    public string Classification { get; set; } = string.Empty;
}

public class ScoreDto
{
    public decimal OverallScore { get; set; }
    public decimal ReturnScore { get; set; }
    public decimal RiskScore { get; set; }
    public decimal DrawdownScore { get; set; }
    public decimal ConsistencyScore { get; set; }
    public decimal TrendScore { get; set; }
    public string Classification { get; set; } = string.Empty;
}

public class YearlyPerformanceDto
{
    public int Year { get; set; }
    public decimal StartPrice { get; set; }
    public decimal EndPrice { get; set; }
    public decimal ReturnPercentage { get; set; }
}

public class ChartDataPointDto
{
    public string Date { get; set; } = string.Empty;
    public decimal Close { get; set; }
    public decimal? Ma50 { get; set; }
    public decimal? Ma200 { get; set; }
    public decimal? Drawdown { get; set; }
}

public class ExplanationsDto
{
    public List<string> PositiveFactors { get; set; } = new();
    public List<string> RiskFactors { get; set; } = new();
    public List<string> TrendFactors { get; set; } = new();
}

public class DataQualityDto
{
    public bool HasFullFiveYearHistory { get; set; }
    public int AnalysisObservationCount { get; set; }
    public int ExcludedObservationCount { get; set; }
    public string FirstValidDate { get; set; } = string.Empty;
    public string LastValidDate { get; set; } = string.Empty;
    public List<string> DataQualityWarnings { get; set; } = new();
}

public class SourceDto
{
    public string Name { get; set; } = "Harvard Dataverse";
    public string Dataset { get; set; } = "Dhaka Stock Exchange Historical Data (1999-2025)";
    public string Doi { get; set; } = "10.7910/DVN/XIFYT1";
}
