using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Admin;
using ShareSync.Infrastructure.Data;

namespace ShareSync.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SectorsController : ControllerBase
{
    private readonly ShareSyncDbContext _context;

    public SectorsController(ShareSyncDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllSectors(CancellationToken cancellationToken)
    {
        var sectors = await _context.Sectors
            .AsNoTracking()
            .OrderBy(s => s.SectorName)
            .Select(s => new AdminSectorDto
            {
                SectorId = s.SectorId,
                SectorName = s.SectorName,
                Description = s.Description,
                CompaniesCount = s.Companies.Count()
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<List<AdminSectorDto>>.Ok(sectors));
    }
}
