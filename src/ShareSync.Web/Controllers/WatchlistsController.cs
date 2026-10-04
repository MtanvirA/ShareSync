using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Watchlists;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class WatchlistsController : ControllerBase
{
    private readonly IWatchlistService _watchlistService;
    private readonly ICurrentUserService _currentUserService;

    public WatchlistsController(IWatchlistService watchlistService, ICurrentUserService currentUserService)
    {
        _watchlistService = watchlistService;
        _currentUserService = currentUserService;
    }

    private int GetCurrentUserId()
    {
        return _currentUserService.UserId ?? throw new UnauthorizedException("User session is invalid.");
    }

    [HttpGet]
    public async Task<IActionResult> GetWatchlists(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _watchlistService.GetUserWatchlistsAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetWatchlist(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _watchlistService.GetWatchlistByIdAsync(id, userId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateWatchlist([FromBody] CreateWatchlistRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _watchlistService.CreateWatchlistAsync(request, userId, cancellationToken);
        return StatusCode(201, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateWatchlist(int id, [FromBody] UpdateWatchlistRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _watchlistService.UpdateWatchlistAsync(id, request, userId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteWatchlist(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _watchlistService.DeleteWatchlistAsync(id, userId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:int}/items")]
    public async Task<IActionResult> AddItem(int id, [FromBody] AddWatchlistItemRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _watchlistService.AddItemAsync(id, request, userId, cancellationToken);
        return StatusCode(201, result);
    }

    [HttpPut("{id:int}/items/{companyId:int}")]
    public async Task<IActionResult> UpdateItem(int id, int companyId, [FromBody] UpdateWatchlistItemRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _watchlistService.UpdateItemAsync(id, companyId, request, userId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}/items/{companyId:int}")]
    public async Task<IActionResult> RemoveItem(int id, int companyId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _watchlistService.RemoveItemAsync(id, companyId, userId, cancellationToken);
        return Ok(result);
    }
}
