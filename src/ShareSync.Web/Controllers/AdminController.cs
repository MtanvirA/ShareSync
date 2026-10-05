using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Admin;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[Authorize(Roles = "ADMIN")]
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IDsePriceService _dsePriceService;
    private readonly ICurrentUserService _currentUserService;

    public AdminController(
        IAdminService adminService,
        IDsePriceService dsePriceService,
        ICurrentUserService currentUserService)
    {
        _adminService = adminService;
        _dsePriceService = dsePriceService;
        _currentUserService = currentUserService;
    }

    private int GetCurrentUserId()
    {
        return _currentUserService.UserId
            ?? throw new AppException("User is not authenticated.", 401);
    }

    // =========================================================================
    // 1. DASHBOARD & SYSTEM METRICS
    // =========================================================================

    [HttpGet("dashboard")]
    [HttpGet("overview")]
    public async Task<IActionResult> GetDashboardStats(CancellationToken cancellationToken)
    {
        var result = await _adminService.GetDashboardStatsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("diagnostics")]
    public async Task<IActionResult> GetSystemDiagnostics(CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            return Unauthorized(ApiResponse.Fail("Authentication required."));
        }

        if (!string.Equals(_currentUserService.Role, "ADMIN", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("Access Denied: Administrative privileges required."));
        }

        var result = await _adminService.GetSystemDiagnosticsAsync(cancellationToken);
        return Ok(result);
    }

    // =========================================================================
    // 2. COMPANY MANAGEMENT
    // =========================================================================

    [HttpGet("companies")]
    public async Task<IActionResult> GetAllCompanies(
        [FromQuery] string? search,
        [FromQuery] int? sectorId,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.GetAllCompaniesAsync(search, sectorId, isActive, cancellationToken);
        return Ok(result);
    }

    [HttpGet("companies/{id:int}")]
    public async Task<IActionResult> GetCompanyById(int id, CancellationToken cancellationToken)
    {
        var result = await _adminService.GetCompanyByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("companies")]
    public async Task<IActionResult> CreateCompany(
        [FromBody] CreateCompanyRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.CreateCompanyAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCompanyById), new { id = result.Data!.CompanyId }, result);
    }

    [HttpPut("companies/{id:int}")]
    public async Task<IActionResult> UpdateCompany(
        int id,
        [FromBody] UpdateCompanyRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.UpdateCompanyAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("companies/{id:int}/toggle-status")]
    [HttpPatch("companies/{id:int}/status")]
    public async Task<IActionResult> ToggleCompanyStatus(int id, CancellationToken cancellationToken)
    {
        var result = await _adminService.ToggleCompanyStatusAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("companies/{id:int}")]
    public async Task<IActionResult> DeleteCompany(int id, CancellationToken cancellationToken)
    {
        var result = await _adminService.DeleteCompanyAsync(id, cancellationToken);
        return Ok(result);
    }

    // =========================================================================
    // 3. SECTOR MANAGEMENT
    // =========================================================================

    [HttpGet("sectors")]
    public async Task<IActionResult> GetAllSectors(CancellationToken cancellationToken)
    {
        var result = await _adminService.GetAllSectorsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("sectors/{id:int}")]
    public async Task<IActionResult> GetSectorById(int id, CancellationToken cancellationToken)
    {
        var result = await _adminService.GetSectorByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("sectors")]
    public async Task<IActionResult> CreateSector(
        [FromBody] CreateSectorRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.CreateSectorAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetSectorById), new { id = result.Data!.SectorId }, result);
    }

    [HttpPut("sectors/{id:int}")]
    public async Task<IActionResult> UpdateSector(
        int id,
        [FromBody] UpdateSectorRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.UpdateSectorAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("sectors/{id:int}")]
    public async Task<IActionResult> DeleteSector(int id, CancellationToken cancellationToken)
    {
        var result = await _adminService.DeleteSectorAsync(id, cancellationToken);
        return Ok(result);
    }

    // =========================================================================
    // 4. USER MANAGEMENT (No Plaintext/Hash Leaks)
    // =========================================================================

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.GetAllUsersAsync(search, role, isActive, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("users/{id:int}/toggle-status")]
    [HttpPatch("users/{id:int}/status")]
    public async Task<IActionResult> ToggleUserStatus(int id, CancellationToken cancellationToken)
    {
        var requestingAdminId = GetCurrentUserId();
        var result = await _adminService.ToggleUserStatusAsync(id, requestingAdminId, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("users/{id:int}/role")]
    public async Task<IActionResult> UpdateUserRole(
        int id,
        [FromBody] UpdateUserRoleRequestDto request,
        CancellationToken cancellationToken)
    {
        var requestingAdminId = GetCurrentUserId();
        var result = await _adminService.UpdateUserRoleAsync(id, request.Role, requestingAdminId, cancellationToken);
        return Ok(result);
    }

    // =========================================================================
    // 5. AUDIT LOGS
    // =========================================================================

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? actionType,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminService.GetAuditLogsAsync(actionType, limit, cancellationToken);
        return Ok(result);
    }

    // =========================================================================
    // 6. DSE MARKET DATA SYNCHRONIZATION
    // =========================================================================

    [HttpGet("dse/status")]
    public async Task<IActionResult> GetDseSyncStatus(CancellationToken cancellationToken)
    {
        var result = await _adminService.GetDseSyncStatusAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("dse/sync")]
    [HttpPost("sync/dse")]
    public async Task<IActionResult> TriggerDseSync(CancellationToken cancellationToken)
    {
        var result = await _dsePriceService.SyncAllCompanyPricesAsync(cancellationToken);
        return Ok(result);
    }
}
