using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Currencies;
using ShareSync.Application.Services;

namespace ShareSync.Web.Controllers;

/// <summary>
/// API: Currencies Controller
/// FEATURE: Multi-Asset and Multi-Currency (LF-29)
/// 
/// Endpoints:
/// GET /api/currencies/supported — Supported currency and asset universe (BDT, USD, EQUITY, ETF, etc.)
/// GET /api/currencies/rates — Active foreign exchange conversion table
/// GET /api/currencies/convert — Dynamic currency converter with source provenance
/// </summary>
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

    /// <summary>
    /// API: GET /api/currencies/supported
    /// MULTI ASSET: Supported Universe
    /// Returns canonical list of supported currencies ("BDT", "USD") and asset types.
    /// </summary>
    [HttpGet("supported")]
    public IActionResult GetSupportedCurrenciesAndAssets()
    {
        var supported = _currencyService.GetSupportedCurrenciesAndAssets();
        return Ok(ApiResponse<SupportedCurrenciesAndAssetsDto>.Ok(supported));
    }

    /// <summary>
    /// API: GET /api/currencies/rates
    /// CURRENCY: Exchange Rate Table
    /// Fetches all active exchange rates from MySQL or documented baseline.
    /// </summary>
    [HttpGet("rates")]
    public async Task<IActionResult> GetRates(CancellationToken cancellationToken = default)
    {
        var rates = await _currencyService.GetExchangeRatesAsync(cancellationToken);
        return Ok(ApiResponse<List<ExchangeRateDto>>.Ok(rates));
    }

    /// <summary>
    /// API: GET /api/currencies/convert
    /// CALCULATION: Currency Conversion
    /// Formula: Converted Amount = Round(Amount * Exchange Rate, 2)
    /// Converts a monetary value between currencies with effective date alignment and data source provenance.
    /// </summary>
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
