using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Dividends;

namespace ShareSync.Application.Interfaces;

public interface IDividendService
{
    Task<ApiResponse<List<DividendDto>>> GetDividendsAsync(DividendFilterDto filter, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<DividendDto>> GetDividendByIdAsync(int dividendId, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<DividendSummaryDto>> GetDividendSummaryAsync(int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<DividendDto>> CreateDividendAsync(CreateDividendRequestDto request, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<DividendDto>> UpdateDividendAsync(int dividendId, UpdateDividendRequestDto request, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse> DeleteDividendAsync(int dividendId, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<DividendAnalyticsDto>> GetDividendAnalyticsAsync(DividendAnalyticsFilterDto filter, int userId, CancellationToken cancellationToken = default);
}
