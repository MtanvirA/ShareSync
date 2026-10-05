using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Goals;

namespace ShareSync.Application.Interfaces;

public interface IPortfolioGoalService
{
    Task<ApiResponse<List<PortfolioGoalDto>>> GetUserGoalsAsync(
        int userId,
        int? portfolioId = null,
        bool activeOnly = false,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PortfolioGoalDto>> GetGoalByIdAsync(
        int goalId,
        int userId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PortfolioGoalDto>> CreateGoalAsync(
        CreatePortfolioGoalRequestDto request,
        int userId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PortfolioGoalDto>> UpdateGoalAsync(
        int goalId,
        UpdatePortfolioGoalRequestDto request,
        int userId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse> DeleteGoalAsync(
        int goalId,
        int userId,
        CancellationToken cancellationToken = default);
}
