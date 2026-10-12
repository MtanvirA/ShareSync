namespace ShareSync.Application.DTOs.Reports;

public class ReportSummaryDto
{
    public decimal TotalPortfolioValue { get; set; }
    public decimal TotalUnrealizedProfitLoss { get; set; }
    public decimal TotalRealizedProfitLoss { get; set; }
    public decimal TotalProfitLoss => Math.Round(TotalUnrealizedProfitLoss + TotalRealizedProfitLoss, 2);
    public decimal TotalFees { get; set; }
    public decimal NetCashFlow { get; set; }
    public decimal TotalDividendIncome { get; set; }
    public int TotalTransactionsCount { get; set; }
}
