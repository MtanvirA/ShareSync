using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.DTOs.Reports;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IReportExportService _reportExportService;

    public ReportsController(IReportService reportService, IReportExportService reportExportService)
    {
        _reportService = reportService;
        _reportExportService = reportExportService;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(claim, out var userId))
        {
            return userId;
        }

        throw new UnauthorizedAccessException("User is not authenticated.");
    }

    /// <summary>
    /// Report 1: Portfolio Holdings Report (current holdings, weighted average cost, unrealized P/L).
    /// </summary>
    [HttpGet("holdings")]
    public async Task<IActionResult> GetHoldingsReport(
        [FromQuery] int? portfolioId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var response = await _reportService.GetHoldingsReportAsync(userId, portfolioId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Report 2: Portfolio Performance Report (historical snapshots, value change, Chart.js dataset).
    /// </summary>
    [HttpGet("performance")]
    public async Task<IActionResult> GetPerformanceReport(
        [FromQuery] int? portfolioId,
        [FromQuery] string? period,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var response = await _reportService.GetPerformanceReportAsync(userId, portfolioId, period, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Report 3: Transaction History Report (activity, cash flow, BUY/SELL filtering).
    /// </summary>
    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactionHistoryReport(
        [FromQuery] int? portfolioId,
        [FromQuery] int? companyId,
        [FromQuery] string? transactionType,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var response = await _reportService.GetTransactionHistoryReportAsync(
            userId, portfolioId, companyId, transactionType, startDate, endDate, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Report 4: Company / Sector Investment Report (investment & allocation by company and sector).
    /// </summary>
    [HttpGet("company-sector")]
    public async Task<IActionResult> GetCompanySectorReport(
        [FromQuery] int? portfolioId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var response = await _reportService.GetCompanySectorReportAsync(userId, portfolioId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Report 5: Dividend Income Report (aggregate dividend income by company and period).
    /// </summary>
    [HttpGet("dividends")]
    public async Task<IActionResult> GetDividendIncomeReport(
        [FromQuery] int? companyId,
        [FromQuery] int? year,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var response = await _reportService.GetDividendIncomeReportAsync(
            userId, companyId, year, startDate, endDate, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Report 6: Watchlist / Target Price Report (target vs current price, spread, target status).
    /// </summary>
    [HttpGet("watchlist")]
    public async Task<IActionResult> GetWatchlistTargetReport(
        [FromQuery] int? watchlistId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var response = await _reportService.GetWatchlistTargetReportAsync(userId, watchlistId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// High-level executive report summary (portfolio value, unrealized P/L, dividends, transactions).
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetReportSummary(
        [FromQuery] int? portfolioId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var response = await _reportService.GetReportSummaryAsync(userId, portfolioId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Export reports in PDF, Excel, or CSV format.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportReport(
        [FromQuery] string reportType,
        [FromQuery] string format,
        [FromQuery] ReportExportFilterDto filter,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _reportExportService.ExportReportAsync(reportType, format, userId, filter, cancellationToken);
        return File(result.FileContents, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Route-based report export endpoint.
    /// </summary>
    [HttpGet("{reportType}/export")]
    public async Task<IActionResult> ExportReportByType(
        string reportType,
        [FromQuery] string format,
        [FromQuery] ReportExportFilterDto filter,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _reportExportService.ExportReportAsync(reportType, format, userId, filter, cancellationToken);
        return File(result.FileContents, result.ContentType, result.FileName);
    }
}
