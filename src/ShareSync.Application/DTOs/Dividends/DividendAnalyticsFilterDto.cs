namespace ShareSync.Application.DTOs.Dividends;

public class DividendAnalyticsFilterDto
{
    public int? PortfolioId { get; set; }
    public int? CompanyId { get; set; }
    public int? Year { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
