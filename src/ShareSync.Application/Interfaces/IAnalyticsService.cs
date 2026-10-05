using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Analytics;

namespace ShareSync.Application.Interfaces;

public interface IAnalyticsService
{
    Task<ApiResponse<PortfolioAnalyticsDto>> GetAnalyticsAsync(
        int userId,
        int? portfolioId = null,
        string? period = "ALL",
        CancellationToken cancellationToken = default);
}
