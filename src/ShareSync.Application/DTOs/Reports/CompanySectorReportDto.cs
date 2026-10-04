namespace ShareSync.Application.DTOs.Reports;

public class SectorAllocationItemDto
{
    public int SectorId { get; set; }
    public string SectorName { get; set; } = string.Empty;
    public int HoldingsCount { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal CurrentMarketValue { get; set; }
    public decimal UnrealizedProfitLoss { get; set; }
    public decimal AllocationPercentage { get; set; }
}

public class CompanyInvestmentItemDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public string SectorName { get; set; } = string.Empty;
    public decimal SharesHeld { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal CurrentMarketValue { get; set; }
    public decimal UnrealizedProfitLoss { get; set; }
    public decimal AllocationPercentage { get; set; }
}

public class CompanySectorReportDto
{
    public decimal TotalPortfolioValue { get; set; }
    public List<SectorAllocationItemDto> Sectors { get; set; } = new();
    public List<CompanyInvestmentItemDto> Companies { get; set; } = new();
}
