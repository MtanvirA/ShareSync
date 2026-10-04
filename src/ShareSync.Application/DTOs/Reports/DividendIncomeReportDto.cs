namespace ShareSync.Application.DTOs.Reports;

public class DividendReportItemDto
{
    public int DividendId { get; set; }
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public decimal DividendPerShare { get; set; }
    public DateTime DeclarationDate { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal UserSharesHeld { get; set; }
    public decimal EstimatedIncome { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CompanyDividendAggregationDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public decimal TotalDividendPerShare { get; set; }
    public decimal TotalIncome { get; set; }
    public int PaymentsCount { get; set; }
}

public class PeriodDividendAggregationDto
{
    public int Year { get; set; }
    public decimal TotalIncome { get; set; }
    public int PaymentsCount { get; set; }
}

public class DividendIncomeReportDto
{
    public decimal TotalIncome { get; set; }
    public decimal ThisYearIncome { get; set; }
    public decimal UpcomingIncome { get; set; }
    public int TotalPaymentsCount { get; set; }
    public List<CompanyDividendAggregationDto> CompanyBreakdown { get; set; } = new();
    public List<PeriodDividendAggregationDto> PeriodBreakdown { get; set; } = new();
    public List<DividendReportItemDto> Dividends { get; set; } = new();
}
