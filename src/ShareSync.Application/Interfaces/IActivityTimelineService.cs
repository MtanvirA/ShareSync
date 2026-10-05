using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Activities;

namespace ShareSync.Application.Interfaces;

public interface IActivityTimelineService
{
    Task<ApiResponse<PagedActivitiesDto>> GetUserTimelineAsync(
        int userId,
        UserActivityFilterDto filter,
        CancellationToken cancellationToken = default);
}
