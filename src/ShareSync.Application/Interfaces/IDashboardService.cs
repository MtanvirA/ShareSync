using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Dashboard;

namespace ShareSync.Application.Interfaces;

public interface IDashboardService
{
    Task<ApiResponse<DashboardResponseDto>> GetDashboardDataAsync(
        int userId,
        int? portfolioId = null,
        string? period = "6",
        CancellationToken cancellationToken = default);
}
