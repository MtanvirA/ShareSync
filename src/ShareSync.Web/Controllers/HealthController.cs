using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Models;
using ShareSync.Infrastructure.Data;

namespace ShareSync.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ShareSyncDbContext _context;
    private readonly ILogger<HealthController> _logger;

    public HealthController(ShareSyncDbContext context, ILogger<HealthController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(ApiResponse.Ok(new
        {
            status = "Healthy",
            service = "ShareSync Backend API",
            timestamp = DateTime.UtcNow
        }));
    }

    [HttpGet("db")]
    public async Task<IActionResult> CheckDatabase(CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return StatusCode(503, ApiResponse.Fail("Unable to establish connection to Oracle Database."));
            }

            var userCount = await _context.Users.CountAsync(cancellationToken);
            var companyCount = await _context.Companies.CountAsync(cancellationToken);
            var sectorCount = await _context.Sectors.CountAsync(cancellationToken);
            var portfolioCount = await _context.Portfolios.CountAsync(cancellationToken);
            var transactionCount = await _context.Transactions.CountAsync(cancellationToken);

            return Ok(ApiResponse.Ok(new
            {
                database = "Oracle Database 23ai Free",
                status = "Connected",
                tables = new
                {
                    users = userCount,
                    sectors = sectorCount,
                    companies = companyCount,
                    portfolios = portfolioCount,
                    transactions = transactionCount
                },
                timestamp = DateTime.UtcNow
            }, "Oracle database connectivity verified successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database connectivity check failed.");
            return StatusCode(500, ApiResponse.Fail($"Database connectivity check failed: {ex.Message}"));
        }
    }
}
