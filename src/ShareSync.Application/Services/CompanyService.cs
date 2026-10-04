using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Companies;
using ShareSync.Application.Interfaces;

namespace ShareSync.Application.Services;

public class CompanyService : ICompanyService
{
    private readonly IApplicationDbContext _context;

    public CompanyService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<CompanyDto>>> GetAllCompaniesAsync(CancellationToken cancellationToken = default)
    {
        var companies = await _context.Companies
            .AsNoTracking()
            .Include(c => c.Sector)
            .OrderBy(c => c.TickerSymbol)
            .Select(c => new CompanyDto
            {
                CompanyId = c.CompanyId,
                CompanyName = c.CompanyName,
                TickerSymbol = c.TickerSymbol,
                SectorId = c.SectorId,
                SectorName = c.Sector != null ? c.Sector.SectorName : string.Empty,
                CurrentPrice = c.CurrentPrice,
                MarketCap = c.MarketCap
            })
            .ToListAsync(cancellationToken);

        return ApiResponse<List<CompanyDto>>.Ok(companies);
    }

    public async Task<ApiResponse<CompanyDto>> GetCompanyByIdAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var company = await _context.Companies
            .AsNoTracking()
            .Include(c => c.Sector)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId, cancellationToken);

        if (company == null)
        {
            throw new NotFoundException("Company", companyId);
        }

        var dto = new CompanyDto
        {
            CompanyId = company.CompanyId,
            CompanyName = company.CompanyName,
            TickerSymbol = company.TickerSymbol,
            SectorId = company.SectorId,
            SectorName = company.Sector != null ? company.Sector.SectorName : string.Empty,
            CurrentPrice = company.CurrentPrice,
            MarketCap = company.MarketCap
        };

        return ApiResponse<CompanyDto>.Ok(dto);
    }
}
