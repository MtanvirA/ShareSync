using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.DTOs.Snapshots;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[ApiController]
[Authorize]
public class SnapshotsController : ControllerBase
{
    private readonly IPortfolioSnapshotService _snapshotService;

    public SnapshotsController(IPortfolioSnapshotService snapshotService)
    {
        _snapshotService = snapshotService;
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
    /// List all snapshots for a specific portfolio with optional date filtering.
    /// </summary>
    [HttpGet("api/portfolios/{portfolioId:int}/snapshots")]
    public async Task<IActionResult> GetPortfolioSnapshots(
        int portfolioId,
        [FromQuery] SnapshotFilterDto filter,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var response = await _snapshotService.GetPortfolioSnapshotsAsync(portfolioId, userId, filter, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Retrieve chronological performance data formatted for charting and reports.
    /// </summary>
    [HttpGet("api/portfolios/{portfolioId:int}/snapshots/performance")]
    public async Task<IActionResult> GetPerformanceData(
        int portfolioId,
        [FromQuery] string? period,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var response = await _snapshotService.GetPerformanceDataAsync(portfolioId, userId, period, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Get details of a single snapshot by ID.
    /// </summary>
    [HttpGet("api/snapshots/{id:int}")]
    public async Task<IActionResult> GetSnapshotById(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var response = await _snapshotService.GetSnapshotByIdAsync(id, userId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Record a new snapshot for a portfolio (auto-calculated from holdings if totalValue is omitted).
    /// </summary>
    [HttpPost("api/portfolios/{portfolioId:int}/snapshots")]
    public async Task<IActionResult> CreateSnapshot(
        int portfolioId,
        [FromBody] CreateSnapshotRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var response = await _snapshotService.CreateSnapshotAsync(portfolioId, request, userId, cancellationToken);
        return StatusCode(201, response);
    }

    /// <summary>
    /// Delete a portfolio snapshot.
    /// </summary>
    [HttpDelete("api/snapshots/{id:int}")]
    public async Task<IActionResult> DeleteSnapshot(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var response = await _snapshotService.DeleteSnapshotAsync(id, userId, cancellationToken);
        return Ok(response);
    }
}
