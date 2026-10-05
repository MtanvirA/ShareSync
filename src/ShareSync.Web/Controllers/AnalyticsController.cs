using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ICurrentUserService _currentUserService;

    public AnalyticsController(
        IAnalyticsService analyticsService,
        ICurrentUserService currentUserService)
    {
        _analyticsService = analyticsService;
        _currentUserService = currentUserService;
    }

    private int GetCurrentUserId()
    {
        return _currentUserService.UserId ?? throw new UnauthorizedException("User session is invalid.");
    }

    /// <summary>
    /// Retrieves comprehensive advanced portfolio analytics including returns, unrealized/realized P/L,
    /// dividend yield, sector & company allocations, concentration metrics, top gainers/losers, and
    /// historical equity performance curve.
    /// </summary>
    /// <param name="portfolioId">Optional portfolio ID to scope analytics. If omitted, aggregates across all user portfolios.</param>
    /// <param name="period">Timeframe for historical performance: 1M, 3M, 6M, 1Y, or ALL (default: ALL).</param>
    [HttpGet]
    public async Task<IActionResult> GetAnalytics(
        [FromQuery] int? portfolioId,
        [FromQuery] string? period,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _analyticsService.GetAnalyticsAsync(userId, portfolioId, period, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves advanced portfolio analytics scoped specifically to a single portfolio.
    /// </summary>
    [HttpGet("/api/portfolios/{portfolioId:int}/analytics")]
    public async Task<IActionResult> GetPortfolioAnalytics(
        int portfolioId,
        [FromQuery] string? period,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _analyticsService.GetAnalyticsAsync(userId, portfolioId, period, cancellationToken);
        return Ok(result);
    }
}
