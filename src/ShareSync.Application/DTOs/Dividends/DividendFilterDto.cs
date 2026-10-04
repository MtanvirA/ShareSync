namespace ShareSync.Application.DTOs.Dividends;

public class DividendFilterDto
{
    public int? CompanyId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? Year { get; set; }
}
