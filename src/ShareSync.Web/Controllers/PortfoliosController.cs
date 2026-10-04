using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Portfolios;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PortfoliosController : ControllerBase
{
    private readonly IPortfolioService _portfolioService;
    private readonly ICurrentUserService _currentUserService;

    public PortfoliosController(IPortfolioService portfolioService, ICurrentUserService currentUserService)
    {
        _portfolioService = portfolioService;
        _currentUserService = currentUserService;
    }

    private int GetCurrentUserId()
    {
        return _currentUserService.UserId ?? throw new UnauthorizedException("User session is invalid.");
    }

    [HttpGet]
    public async Task<IActionResult> GetUserPortfolios(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _portfolioService.GetUserPortfoliosAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetPortfolio(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _portfolioService.GetPortfolioByIdAsync(id, userId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePortfolio([FromBody] CreatePortfolioRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var userId = GetCurrentUserId();
        var result = await _portfolioService.CreatePortfolioAsync(request, userId, cancellationToken);
        return CreatedAtAction(nameof(GetPortfolio), new { id = result.Data!.PortfolioId }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdatePortfolio(int id, [FromBody] UpdatePortfolioRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var userId = GetCurrentUserId();
        var result = await _portfolioService.UpdatePortfolioAsync(id, request, userId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeletePortfolio(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _portfolioService.DeletePortfolioAsync(id, userId, cancellationToken);
        return Ok(result);
    }
}
