using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Watchlists;

namespace ShareSync.Application.Interfaces;

public interface IWatchlistService
{
    Task<ApiResponse<List<WatchlistDto>>> GetUserWatchlistsAsync(int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<WatchlistDetailDto>> GetWatchlistByIdAsync(int watchlistId, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<WatchlistDto>> CreateWatchlistAsync(CreateWatchlistRequestDto request, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<WatchlistDto>> UpdateWatchlistAsync(int watchlistId, UpdateWatchlistRequestDto request, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse> DeleteWatchlistAsync(int watchlistId, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<WatchlistItemDto>> AddItemAsync(int watchlistId, AddWatchlistItemRequestDto request, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<WatchlistItemDto>> UpdateItemAsync(int watchlistId, int companyId, UpdateWatchlistItemRequestDto request, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse> RemoveItemAsync(int watchlistId, int companyId, int userId, CancellationToken cancellationToken = default);
}
