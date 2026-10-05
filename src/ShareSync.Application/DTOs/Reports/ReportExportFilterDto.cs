namespace ShareSync.Application.DTOs.Reports;

public class ReportExportFilterDto
{
    public int? PortfolioId { get; set; }
    public int? CompanyId { get; set; }
    public string? TransactionType { get; set; }
    public int? Year { get; set; }
    public string? Period { get; set; }
    public int? WatchlistId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
