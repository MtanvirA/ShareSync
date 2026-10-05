using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Companies;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;
    private readonly IDsePriceService _dsePriceService;
    private readonly ICurrentUserService _currentUserService;

    public CompaniesController(
        ICompanyService companyService,
        IDsePriceService dsePriceService,
        ICurrentUserService currentUserService)
    {
        _companyService = companyService;
        _dsePriceService = dsePriceService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllCompanies(CancellationToken cancellationToken)
    {
        var result = await _companyService.GetAllCompaniesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCompany(int id, CancellationToken cancellationToken)
    {
        var result = await _companyService.GetCompanyByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}/detail")]
    public async Task<IActionResult> GetCompanyDetail(int id, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new AppException("User is not authenticated.", 401);

        var result = await _companyService.GetCompanyDetailAsync(id, userId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("sync-prices")]
    public async Task<IActionResult> SyncDsePrices(CancellationToken cancellationToken)
    {
        var result = await _dsePriceService.SyncAllCompanyPricesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}/price-history")]
    public async Task<IActionResult> GetPriceHistory(
        int id,
        [FromQuery] CompanyPriceHistoryFilterDto? filter,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.GetCompanyPriceHistoryAsync(id, filter, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all 400+ companies listed on the Dhaka Stock Exchange with market caps, sectors, and local database tracking status.
    /// </summary>
    [HttpGet("dse-market-list")]
    public async Task<IActionResult> GetDseMarketList(CancellationToken cancellationToken)
    {
        var list = await _dsePriceService.GetDseListedCompaniesAsync(cancellationToken);
        return Ok(ApiResponse<List<DseListedCompanyDto>>.Ok(list));
    }

    /// <summary>
    /// Imports and adds any Dhaka Stock Exchange listed company into ShareSync by ticker symbol with live quote and sector reference.
    /// </summary>
    [HttpPost("add-from-dse")]
    public async Task<IActionResult> AddCompanyFromDse(
        [FromBody] AddDseCompanyRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _dsePriceService.ImportCompanyFromDseAsync(
            request.Symbol,
            request.Name,
            request.Sector,
            cancellationToken);

        return Ok(ApiResponse<CompanyDto>.Ok(result, $"Successfully added {result.TickerSymbol} ({result.CompanyName}) to ShareSync."));
    }
}
