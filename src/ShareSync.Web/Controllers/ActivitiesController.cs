using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.DTOs.Activities;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ActivitiesController : ControllerBase
{
    private readonly IActivityTimelineService _activityService;
    private readonly ICurrentUserService _currentUserService;

    public ActivitiesController(
        IActivityTimelineService activityService,
        ICurrentUserService currentUserService)
    {
        _activityService = activityService;
        _currentUserService = currentUserService;
    }

    private int GetCurrentUserId()
    {
        return _currentUserService.UserId ?? throw new UnauthorizedException("User session is invalid.");
    }

    /// <summary>
    /// Retrieves a chronological, normalized activity timeline of portfolio and account events for the authenticated user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTimeline(
        [FromQuery] UserActivityFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _activityService.GetUserTimelineAsync(userId, filter, cancellationToken);
        return Ok(result);
    }
}
