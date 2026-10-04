using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Portfolios;

namespace ShareSync.Application.Interfaces;

public interface IPortfolioService
{
    Task<ApiResponse<List<PortfolioSummaryDto>>> GetUserPortfoliosAsync(int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PortfolioDetailDto>> GetPortfolioByIdAsync(int portfolioId, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PortfolioSummaryDto>> CreatePortfolioAsync(CreatePortfolioRequestDto request, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PortfolioSummaryDto>> UpdatePortfolioAsync(int portfolioId, UpdatePortfolioRequestDto request, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse> DeletePortfolioAsync(int portfolioId, int userId, CancellationToken cancellationToken = default);
}
