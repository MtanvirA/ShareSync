using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Benchmarks;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BenchmarksController : ControllerBase
{
    private readonly IBenchmarkService _benchmarkService;
    private readonly ICurrentUserService _currentUserService;

    public BenchmarksController(IBenchmarkService benchmarkService, ICurrentUserService currentUserService)
    {
        _benchmarkService = benchmarkService;
        _currentUserService = currentUserService;
    }

    private int GetCurrentUserId()
    {
        return _currentUserService.UserId ?? throw new UnauthorizedException("User session is invalid.");
    }

    [HttpGet]
    public async Task<IActionResult> GetBenchmarks(CancellationToken cancellationToken)
    {
        var result = await _benchmarkService.GetAvailableBenchmarksAsync(cancellationToken);
        return Ok(ApiResponse<List<BenchmarkDto>>.Ok(result));
    }

    [HttpGet("compare")]
    public async Task<IActionResult> Compare([FromQuery] BenchmarkComparisonRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _benchmarkService.ComparePortfolioAsync(request, userId, cancellationToken);
        return Ok(ApiResponse<BenchmarkComparisonDto>.Ok(result));
    }
}
