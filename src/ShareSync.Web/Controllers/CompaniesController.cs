using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;
    private readonly IDsePriceService _dsePriceService;

    public CompaniesController(
        ICompanyService companyService,
        IDsePriceService dsePriceService)
    {
        _companyService = companyService;
        _dsePriceService = dsePriceService;
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

    [HttpPost("sync-prices")]
    public async Task<IActionResult> SyncDsePrices(CancellationToken cancellationToken)
    {
        var result = await _dsePriceService.SyncAllCompanyPricesAsync(cancellationToken);
        return Ok(result);
    }
}
