using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Inflation;
using ShareSync.Application.Services;

namespace ShareSync.Web.Controllers;

/// <summary>
/// API: Inflation Controller
/// FEATURE: Inflation and Real Return
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

    /// <summary>
    /// API: GET /api/inflation/cpi-observations
    /// INFLATION: Monthly CPI Observation Levels
    /// Sourced from official statistical releases (e.g., BBS Base 2021-22=100).
    /// </summary>
    [HttpGet("cpi-observations")]
    public async Task<IActionResult> GetCpiObservations(
        [FromQuery] string? country = "BD",
        CancellationToken cancellationToken = default)
    {
        var observations = await _inflationService.GetCpiObservationsAsync(country ?? "BD", cancellationToken);
        return Ok(ApiResponse<List<CpiIndexObservationDto>>.Ok(observations));
    }

    /// <summary>
    /// API: GET /api/inflation/real-return-period
    /// CALCULATION: Date-Range Real Return using Monthly CPI Index levels
    /// </summary>
    [HttpGet("real-return-period")]
    public async Task<IActionResult> CalculateRealReturnPeriod(
        [FromQuery] decimal nominalReturn,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? country = "BD",
        CancellationToken cancellationToken = default)
    {
        var end = endDate ?? DateTime.UtcNow;
        var result = await _inflationService.CalculateRealReturnForDateRangeAsync(
            country ?? "BD",
            nominalReturn,
            startDate,
            end,
            cancellationToken);

        return Ok(ApiResponse<RealReturnCalculationResultDto>.Ok(result));
    }
}
