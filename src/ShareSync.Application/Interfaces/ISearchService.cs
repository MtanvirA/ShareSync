using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Search;

namespace ShareSync.Application.Interfaces;

public interface ISearchService
{
    Task<ApiResponse<GlobalSearchResultDto>> SearchAsync(
        string? query,
        int userId,
        int limitPerCategory = 5,
        CancellationToken cancellationToken = default);
}
