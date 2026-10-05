using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.DTOs.Notifications;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserService _currentUserService;

    public NotificationsController(
        INotificationService notificationService,
        ICurrentUserService currentUserService)
    {
        _notificationService = notificationService;
        _currentUserService = currentUserService;
    }

    private int GetCurrentUserId()
    {
        return _currentUserService.UserId
            ?? throw new AppException("User is not authenticated.", 401);
    }

    [HttpGet]
    public async Task<IActionResult> GetUserNotifications(
        [FromQuery] bool? unreadOnly,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _notificationService.GetUserNotificationsAsync(userId, unreadOnly, limit, cancellationToken);
        return Ok(result);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _notificationService.GetUnreadCountAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetNotificationSummary(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _notificationService.GetNotificationSummaryAsync(userId, limit, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateNotification(
        [FromBody] CreateNotificationDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        request.UserId = userId; // Always tie to current user
        var result = await _notificationService.CreateNotificationAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _notificationService.MarkAsReadAsync(id, userId, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _notificationService.MarkAllAsReadAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteNotification(int id, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _notificationService.DeleteNotificationAsync(id, userId, cancellationToken);
        return Ok(result);
    }
}
