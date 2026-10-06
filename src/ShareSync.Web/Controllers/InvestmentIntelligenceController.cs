using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.InvestmentIntelligence;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/investment-analysis")]
public class InvestmentIntelligenceController : ControllerBase
{
    private readonly IInvestmentIntelligenceService _intelligenceService;

    public InvestmentIntelligenceController(IInvestmentIntelligenceService intelligenceService)
    {
        _intelligenceService = intelligenceService;
    }

    [HttpGet("company/{companyId}")]
    public async Task<ActionResult<ApiResponse<HistoricalInvestmentProfileDto>>> GetHistoricalInvestmentProfile(
        int companyId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var result = await _intelligenceService.GetHistoricalInvestmentProfileAsync(companyId, fromDate, toDate, cancellationToken);
        return Ok(result);
    }
}
