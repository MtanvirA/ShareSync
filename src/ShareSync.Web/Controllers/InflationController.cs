using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Inflation;
using ShareSync.Application.Services;

namespace ShareSync.Web.Controllers;

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

    [HttpGet("rates")]
    public async Task<IActionResult> GetHistoricalRates(
        [FromQuery] string? country = "BD",
        CancellationToken cancellationToken = default)
    {
        var rates = await _inflationService.GetHistoricalRatesAsync(country ?? "BD", cancellationToken);
        return Ok(ApiResponse<List<InflationRateDto>>.Ok(rates));
    }

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
