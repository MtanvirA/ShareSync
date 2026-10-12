using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.InvestmentIntelligence;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

/// <summary>
/// API: Investment Intelligence Controller
/// FEATURE: Investment Intelligence
/// 
/// Endpoints:
/// GET /api/investment-analysis/company/{companyId}
/// 
/// Evaluates multi-year historical price trajectories from the MySQL Harvard Dataverse / DSE archive.
/// Computes quantitative risk and performance metrics: CAGR, Annualized Volatility, Maximum Drawdown,
/// SMA 50 / SMA 200, Golden/Death Cross detection, and composite 100-point Investment Score.
/// 
/// SECURITY: Authorization
/// Protected by JWT Bearer token authentication.
/// </summary>
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

    /// <summary>
    /// API: GET /api/investment-analysis/company/{companyId}
    /// INVESTMENT INTELLIGENCE: Analysis
    /// INVESTMENT INTELLIGENCE: Five-Year Window
    /// INVESTMENT INTELLIGENCE: Score
    /// 
    /// Generates deterministic historical investment evaluation for the target company across optional custom or default 5-year window.
    /// </summary>
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
