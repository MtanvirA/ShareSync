namespace ShareSync.Application.DTOs.Portfolios;

public class PortfolioDetailDto : PortfolioSummaryDto
{
    public List<PortfolioHoldingDto> Holdings { get; set; } = new();
}
