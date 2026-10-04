using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Companies;

namespace ShareSync.Application.Interfaces;

public interface ICompanyService
{
    Task<ApiResponse<List<CompanyDto>>> GetAllCompaniesAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<CompanyDto>> GetCompanyByIdAsync(int companyId, CancellationToken cancellationToken = default);
}
