using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Inflation;
using ShareSync.Application.Services;

namespace ShareSync.Web.Controllers;

/// <summary>
/// API: Inflation Controller
/// FEATURE: Inflation-Adjusted Return (LF-28)
/// 
/// Endpoints:
/// GET /api/inflation/rates — Chronological CPI inflation rates for BD and US
/// GET /api/inflation/real-return — Fisher equation purchasing power return adjustments
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class InflationController : ControllerBase
{
    private readonly IInflationService _inflationService;

    public InflationController(IInflationService inflationService)
    {
        _inflationService = inflationService;
    }

    /// <summary>
    /// API: GET /api/inflation/rates
    /// INFLATION: CPI Data Retrieval
    /// Returns annual inflation percentages from BBS / BLS series.
    /// </summary>
    [HttpGet("rates")]
    public async Task<IActionResult> GetHistoricalRates(
        [FromQuery] string? country = "BD",
        CancellationToken cancellationToken = default)
    {
        var rates = await _inflationService.GetHistoricalRatesAsync(country ?? "BD", cancellationToken);
        return Ok(ApiResponse<List<InflationRateDto>>.Ok(rates));
    }

    /// <summary>
    /// API: GET /api/inflation/real-return
    /// CALCULATION: Real Return (Fisher Equation)
    /// Formula: Real Return (%) = [ ( (1 + Nominal / 100) / (1 + Cumulative Inflation / 100) ) - 1 ] * 100
    /// Computes real purchasing power gain/loss over specified multi-year periods.
    /// </summary>
    [HttpGet("real-return")]
    public async Task<IActionResult> CalculateRealReturn(
        [FromQuery] decimal nominalReturn,
        [FromQuery] int startYear,
        [FromQuery] int endYear,
        [FromQuery] string? country = "BD",
        CancellationToken cancellationToken = default)
    {
        var result = await _inflationService.CalculateRealReturnAsync(
            nominalReturn,
            startYear,
            endYear,
            country ?? "BD",
            cancellationToken);

        return Ok(ApiResponse<RealReturnCalculationResultDto>.Ok(result));
    }
}
