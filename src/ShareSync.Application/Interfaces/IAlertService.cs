using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Alerts;

namespace ShareSync.Application.Interfaces;

public interface IAlertService
{
    Task<ApiResponse<List<AlertDto>>> GetUserAlertsAsync(
        int userId,
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AlertDto>> GetAlertByIdAsync(
        int alertId,
        int userId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AlertDto>> CreateAlertAsync(
        CreateAlertRequestDto request,
        int userId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AlertDto>> UpdateAlertAsync(
        int alertId,
        UpdateAlertRequestDto request,
        int userId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AlertDto>> ToggleAlertAsync(
        int alertId,
        int userId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> DeleteAlertAsync(
        int alertId,
        int userId,
        CancellationToken cancellationToken = default);

    Task<int> EvaluatePriceAlertsAsync(CancellationToken cancellationToken = default);

    Task<int> EvaluatePortfolioAlertsAsync(
        int? portfolioId = null,
        CancellationToken cancellationToken = default);

    Task<int> EvaluateAllAlertsAsync(CancellationToken cancellationToken = default);
}
