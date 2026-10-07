using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Currencies;
using ShareSync.Application.Services;

namespace ShareSync.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CurrenciesController : ControllerBase
{
    private readonly ICurrencyService _currencyService;

    public CurrenciesController(ICurrencyService currencyService)
    {
        _currencyService = currencyService;
    }

    [HttpGet("supported")]
    public IActionResult GetSupportedCurrenciesAndAssets()
    {
        var supported = _currencyService.GetSupportedCurrenciesAndAssets();
        return Ok(ApiResponse<SupportedCurrenciesAndAssetsDto>.Ok(supported));
    }

    [HttpGet("rates")]
    public async Task<IActionResult> GetRates(CancellationToken cancellationToken = default)
    {
        var rates = await _currencyService.GetExchangeRatesAsync(cancellationToken);
        return Ok(ApiResponse<List<ExchangeRateDto>>.Ok(rates));
    }

    [HttpGet("convert")]
    public async Task<IActionResult> Convert(
        [FromQuery] decimal amount,
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] DateTime? asOfDate = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _currencyService.ConvertWithDetailsAsync(amount, from, to, asOfDate, cancellationToken);
        return Ok(ApiResponse<CurrencyConversionResultDto>.Ok(result));
    }
}
