namespace ShareSync.Application.DTOs.Companies;

public class CompanyDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public int SectorId { get; set; }
    public string SectorName { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal? MarketCap { get; set; }
}
