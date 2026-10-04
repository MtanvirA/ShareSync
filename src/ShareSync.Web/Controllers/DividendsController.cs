using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.DTOs.Dividends;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DividendsController : ControllerBase
{
    private readonly IDividendService _dividendService;
    private readonly ICurrentUserService _currentUserService;

    public DividendsController(IDividendService dividendService, ICurrentUserService currentUserService)
    {
        _dividendService = dividendService;
        _currentUserService = currentUserService;
    }

    private int GetCurrentUserId()
    {
        return _currentUserService.UserId ?? throw new UnauthorizedException("User session is invalid.");
    }

    [HttpGet]
    public async Task<IActionResult> GetDividends([FromQuery] DividendFilterDto filter, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _dividendService.GetDividendsAsync(filter, userId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _dividendService.GetDividendSummaryAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDividend(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _dividendService.GetDividendByIdAsync(id, userId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDividend([FromBody] CreateDividendRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _dividendService.CreateDividendAsync(request, userId, cancellationToken);
        return StatusCode(201, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateDividend(int id, [FromBody] UpdateDividendRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _dividendService.UpdateDividendAsync(id, request, userId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteDividend(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _dividendService.DeleteDividendAsync(id, userId, cancellationToken);
        return Ok(result);
    }
}
