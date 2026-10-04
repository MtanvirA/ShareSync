namespace ShareSync.Application.DTOs.Reports;

public class ReportSummaryDto
{
    public decimal TotalPortfolioValue { get; set; }
    public decimal TotalUnrealizedProfitLoss { get; set; }
    public decimal TotalDividendIncome { get; set; }
    public int TotalTransactionsCount { get; set; }
}
