using ShareSync.Application.Common.Models;

namespace ShareSync.Application.Interfaces;

public class DseQuoteData
{
    public decimal Price { get; set; }
    public decimal? OpenPrice { get; set; }
    public decimal? HighPrice { get; set; }
    public decimal? LowPrice { get; set; }
    public long? Volume { get; set; }
}

public class DsePriceUpdateResultDto
{
    public int UpdatedCount { get; set; }
    public int HistoryCount { get; set; }
    public DateTime SyncedAt { get; set; }
    public Dictionary<string, decimal> UpdatedPrices { get; set; } = new();
}

public class DseListedCompanyDto
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public string? Category { get; set; }
    public decimal? MarketCapMn { get; set; }
    public bool IsAdded { get; set; }
    public int? LocalCompanyId { get; set; }
    public decimal? LocalPrice { get; set; }
}

public class AddDseCompanyRequestDto
{
    public string Symbol { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Sector { get; set; }
}

public interface IDsePriceService
{
    Task<decimal?> FetchLatestPriceAsync(string tickerSymbol, CancellationToken cancellationToken = default);
    Task<DseQuoteData?> FetchQuoteDetailsAsync(string tickerSymbol, CancellationToken cancellationToken = default);
    Task<ApiResponse<DsePriceUpdateResultDto>> SyncAllCompanyPricesAsync(CancellationToken cancellationToken = default);
    Task<List<DseListedCompanyDto>> GetDseListedCompaniesAsync(CancellationToken cancellationToken = default);
    Task<DTOs.Companies.CompanyDto> ImportCompanyFromDseAsync(string symbol, string? preferredName = null, string? preferredSector = null, CancellationToken cancellationToken = default);
}

