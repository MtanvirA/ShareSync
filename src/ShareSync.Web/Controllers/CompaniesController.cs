using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Companies;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

/// <summary>
/// API: Companies Controller
/// DSE: Company Catalog & Market Data Gateway (LF-31)
/// 
/// Endpoints:
/// GET /api/companies — Local canonical tracked companies
/// GET /api/companies/{id} — Single company metadata and quote
/// POST /api/companies/sync-prices — Bulk sync current prices from DSE / StockChartBD
/// GET /api/companies/{id}/price-history — Historical OHLC price series
/// GET /api/companies/dse-market-list — Complete 400+ DSE company catalog
/// GET /api/companies/search — Real-time ticker search and autocomplete
/// POST /api/companies/add-from-dse — Import new DSE ticker into active tracking
/// </summary>
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

    /// <summary>
    /// API: POST /api/companies/sync-prices
    /// DSE: Price Synchronization
    /// Triggers live market price synchronization across all active companies from StockChartBD feed.
    /// </summary>
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

    [HttpGet("search")]
    public async Task<IActionResult> SearchDseCompanies([FromQuery] string q, [FromQuery] int limit = 10, CancellationToken cancellationToken = default)
    {
        var list = await _dsePriceService.GetDseListedCompaniesAsync(cancellationToken);
        var term = q?.Trim() ?? "";

        var filtered = list.Where(c => c.Symbol.Contains(term, StringComparison.OrdinalIgnoreCase) || 
                                       c.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                           .OrderBy(c => c.Symbol.StartsWith(term, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                           .ThenBy(c => c.Symbol)
                           .Take(limit)
                           .ToList();

        return Ok(ApiResponse<List<DseListedCompanyDto>>.Ok(filtered));
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
