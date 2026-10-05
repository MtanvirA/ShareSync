using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;
    private readonly ICurrentUserService _currentUserService;

    public SearchController(
        ISearchService searchService,
        ICurrentUserService currentUserService)
    {
        _searchService = searchService;
        _currentUserService = currentUserService;
    }

    private int GetCurrentUserId()
    {
        return _currentUserService.UserId ?? throw new UnauthorizedException("User session is invalid.");
    }

    /// <summary>
    /// Executes a categorized global search across companies, sectors, and the authenticated user's portfolios and watchlists.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _searchService.SearchAsync(q, userId, limit, cancellationToken);
        return Ok(result);
    }
}
