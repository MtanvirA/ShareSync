using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Benchmarks;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

/// <summary>
/// API: Benchmarks Controller
/// FEATURE: Benchmark Analysis
/// 
/// Routes: /api/benchmarks
/// Compares investor portfolio returns against Dhaka Stock Exchange (DSE) benchmarks (DSEX, DS30, DSES).
/// </summary>
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

    /// <summary>
    /// API: GET /api/benchmarks
    /// BENCHMARK: Index Catalog
    /// Retrieves all available DSE market indices and current index points.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBenchmarks(CancellationToken cancellationToken)
    {
        var result = await _benchmarkService.GetAvailableBenchmarksAsync(cancellationToken);
        return Ok(ApiResponse<List<BenchmarkDto>>.Ok(result));
    }

    /// <summary>
    /// API: GET /api/benchmarks/compare
    /// FEATURE: Benchmark Comparison (LF-30)
    /// BENCHMARK: Active Return
    /// CALCULATION: Outperformance
    /// 
    /// Performs date-aligned comparison between portfolio snapshots and benchmark closing indices over requested timeframe.
    /// </summary>
    [HttpGet("compare")]
    public async Task<IActionResult> Compare([FromQuery] BenchmarkComparisonRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _benchmarkService.ComparePortfolioAsync(request, userId, cancellationToken);
        return Ok(ApiResponse<BenchmarkComparisonDto>.Ok(result));
    }
}
