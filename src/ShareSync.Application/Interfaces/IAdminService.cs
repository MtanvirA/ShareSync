using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Admin;

namespace ShareSync.Application.Interfaces;

public interface IAdminService
{
    // 1. Dashboard & Statistics
    Task<ApiResponse<AdminDashboardDto>> GetDashboardStatsAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<SystemDiagnosticsDto>> GetSystemDiagnosticsAsync(CancellationToken cancellationToken = default);

    // 2. Company Management
    Task<ApiResponse<List<AdminCompanyDto>>> GetAllCompaniesAsync(string? search = null, int? sectorId = null, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<AdminCompanyDto>> GetCompanyByIdAsync(int companyId, CancellationToken cancellationToken = default);
    Task<ApiResponse<AdminCompanyDto>> CreateCompanyAsync(CreateCompanyRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<AdminCompanyDto>> UpdateCompanyAsync(int companyId, UpdateCompanyRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<AdminCompanyDto>> ToggleCompanyStatusAsync(int companyId, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteCompanyAsync(int companyId, CancellationToken cancellationToken = default);

    // 3. Sector Management
    Task<ApiResponse<List<AdminSectorDto>>> GetAllSectorsAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<AdminSectorDto>> GetSectorByIdAsync(int sectorId, CancellationToken cancellationToken = default);
    Task<ApiResponse<AdminSectorDto>> CreateSectorAsync(CreateSectorRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<AdminSectorDto>> UpdateSectorAsync(int sectorId, UpdateSectorRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteSectorAsync(int sectorId, CancellationToken cancellationToken = default);

    // 4. User Management
    Task<ApiResponse<List<AdminUserDto>>> GetAllUsersAsync(string? search = null, string? role = null, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<AdminUserDto>> ToggleUserStatusAsync(int userId, int requestingAdminUserId, CancellationToken cancellationToken = default);
    Task<ApiResponse<AdminUserDto>> UpdateUserRoleAsync(int userId, string newRole, int requestingAdminUserId, CancellationToken cancellationToken = default);

    // 5. Audit Logs
    Task<ApiResponse<List<AdminAuditLogDto>>> GetAuditLogsAsync(string? actionType = null, int limit = 100, CancellationToken cancellationToken = default);

    // 6. Market Data Sync Status
    Task<ApiResponse<object>> GetDseSyncStatusAsync(CancellationToken cancellationToken = default);
}
