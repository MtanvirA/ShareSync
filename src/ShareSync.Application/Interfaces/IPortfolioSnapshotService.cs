using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Snapshots;

namespace ShareSync.Application.Interfaces;

public interface IPortfolioSnapshotService
{
    Task<ApiResponse<List<PortfolioSnapshotDto>>> GetPortfolioSnapshotsAsync(int portfolioId, int userId, SnapshotFilterDto? filter = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<PortfolioSnapshotDto>> GetSnapshotByIdAsync(int snapshotId, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PortfolioSnapshotDto>> CreateSnapshotAsync(int portfolioId, CreateSnapshotRequestDto request, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse> DeleteSnapshotAsync(int snapshotId, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PortfolioPerformanceDataDto>> GetPerformanceDataAsync(int portfolioId, int userId, string? period = null, CancellationToken cancellationToken = default);
}
