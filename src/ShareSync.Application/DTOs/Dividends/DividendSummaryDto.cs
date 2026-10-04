namespace ShareSync.Application.DTOs.Dividends;

public class DividendSummaryDto
{
    public decimal TotalIncome { get; set; }
    public decimal ThisYearIncome { get; set; }
    public decimal UpcomingIncome { get; set; }
    public int CompaniesCount { get; set; }
    public List<CompanyDividendSummaryDto> CompanySummaries { get; set; } = new();
}

public class CompanyDividendSummaryDto
{
    public int CompanyId { get; set; }
    public string TickerSymbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal TotalDividendPerShare { get; set; }
    public decimal UserSharesHeld { get; set; }
    public decimal TotalEstimatedIncome { get; set; }
    public int DividendsCount { get; set; }
}
