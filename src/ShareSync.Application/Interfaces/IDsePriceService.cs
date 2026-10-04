using ShareSync.Application.Common.Models;

namespace ShareSync.Application.Interfaces;

public class DsePriceUpdateResultDto
{
    public int UpdatedCount { get; set; }
    public DateTime SyncedAt { get; set; }
    public Dictionary<string, decimal> UpdatedPrices { get; set; } = new();
}

public interface IDsePriceService
{
    Task<decimal?> FetchLatestPriceAsync(string tickerSymbol, CancellationToken cancellationToken = default);
    Task<ApiResponse<DsePriceUpdateResultDto>> SyncAllCompanyPricesAsync(CancellationToken cancellationToken = default);
}
