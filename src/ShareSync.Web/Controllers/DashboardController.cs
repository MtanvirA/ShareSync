using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(claim, out var userId))
        {
            return userId;
        }

        throw new UnauthorizedAccessException("User is not authenticated.");
    }

    /// <summary>
    /// Retrieves complete unified dashboard data for the authenticated user,
    /// including summary metrics, recent activity, top positions, performance chart history,
    /// and sector allocation.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] int? portfolioId,
        [FromQuery] string? period,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var response = await _dashboardService.GetDashboardDataAsync(userId, portfolioId, period, cancellationToken);
        return Ok(response);
    }
}
