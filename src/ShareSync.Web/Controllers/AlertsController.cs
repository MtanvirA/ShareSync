using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Alerts;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly IAlertService _alertService;
    private readonly ICurrentUserService _currentUserService;

    public AlertsController(
        IAlertService alertService,
        ICurrentUserService currentUserService)
    {
        _alertService = alertService;
        _currentUserService = currentUserService;
    }

    private int GetCurrentUserId()
    {
        return _currentUserService.UserId ?? throw new UnauthorizedException("User session is invalid.");
    }

    /// <summary>
    /// Retrieves all alerts configured by the authenticated user.
    /// Supports status filtering: all, active, triggered, or disabled.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _alertService.GetUserAlertsAsync(userId, status, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a specific alert by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAlertById(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _alertService.GetAlertByIdAsync(id, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new price or portfolio threshold alert.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateAlert(
        [FromBody] CreateAlertRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _alertService.CreateAlertAsync(request, userId, cancellationToken);
        return CreatedAtAction(nameof(GetAlertById), new { id = result.Data!.AlertId }, result);
    }

    /// <summary>
    /// Updates the threshold or state of an existing alert.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAlert(
        int id,
        [FromBody] UpdateAlertRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _alertService.UpdateAlertAsync(id, request, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Toggles the active/disabled status of an alert, or reactivates a triggered alert.
    /// </summary>
    [HttpPatch("{id:int}/toggle")]
    [HttpPut("{id:int}/toggle")]
    public async Task<IActionResult> ToggleAlert(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _alertService.ToggleAlertAsync(id, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Permanently deletes an alert.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAlert(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _alertService.DeleteAlertAsync(id, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Triggers an immediate evaluation pass for all active alerts.
    /// </summary>
    [HttpPost("evaluate")]
    public async Task<IActionResult> EvaluateAlerts(CancellationToken cancellationToken)
    {
        var triggeredCount = await _alertService.EvaluateAllAlertsAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { triggeredCount }, $"Alert evaluation completed. {triggeredCount} alerts triggered."));
    }
}
