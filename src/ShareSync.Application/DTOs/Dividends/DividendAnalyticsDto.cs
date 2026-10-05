namespace ShareSync.Application.DTOs.Dividends;

public class DividendAnalyticsDto
{
    // Context
    public int? PortfolioId { get; set; }
    public string PortfolioName { get; set; } = "All Portfolios";

    // 1. High-Level Income Metrics
    public decimal TotalDividendIncome { get; set; }
    public decimal ThisYearIncome { get; set; }
    public decimal ThisMonthIncome { get; set; }
    public decimal UpcomingIncome { get; set; }
    public decimal AverageDividendYield { get; set; }
    public int TotalPaymentsCount { get; set; }
    public int CompaniesCount { get; set; }

    // 2. Monthly Income (for Monthly Dividend Income Chart)
    public List<MonthlyDividendIncomeDto> MonthlyIncome { get; set; } = new();

    // 3. Yearly Income
    public List<YearlyDividendIncomeDto> YearlyIncome { get; set; } = new();

    // 4. Company Breakdown (for Dividend by Company Chart & Table)
    public List<CompanyDividendAnalyticsDto> CompanyBreakdown { get; set; } = new();

    // 5. Sector Breakdown (Dividend income by sector)
    public List<SectorDividendAnalyticsDto> SectorBreakdown { get; set; } = new();

    // 6. Upcoming Dividends (Calendar / List)
    public List<UpcomingDividendEventDto> UpcomingDividends { get; set; } = new();

    // 7. Filtered Dividend History
    public List<DividendDto> DividendHistory { get; set; } = new();
}

public class MonthlyDividendIncomeDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public int PaymentsCount { get; set; }
}

public class YearlyDividendIncomeDto
{
    public int Year { get; set; }
    public decimal Income { get; set; }
    public int PaymentsCount { get; set; }
}

public class CompanyDividendAnalyticsDto
{
    public int CompanyId { get; set; }
    public string TickerSymbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string SectorName { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
    public decimal TotalDividendPerShare { get; set; }
    public decimal UserSharesHeld { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal MarketValue { get; set; }
    public decimal DividendYield { get; set; }
    public decimal AllocationPercentage { get; set; }
    public int PaymentsCount { get; set; }
}

public class SectorDividendAnalyticsDto
{
    public int? SectorId { get; set; }
    public string SectorName { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
    public decimal AllocationPercentage { get; set; }
    public int PaymentsCount { get; set; }
    public int CompaniesCount { get; set; }
}

public class UpcomingDividendEventDto
{
    public int DividendId { get; set; }
    public int CompanyId { get; set; }
    public string TickerSymbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal DividendPerShare { get; set; }
    public DateTime DeclarationDate { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal UserSharesHeld { get; set; }
    public decimal EstimatedIncome { get; set; }
    public int DaysUntilPayment { get; set; }
}
