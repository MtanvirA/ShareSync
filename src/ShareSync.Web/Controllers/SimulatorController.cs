using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.DTOs.Simulator;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SimulatorController : ControllerBase
{
    private readonly ISimulatorService _simulatorService;
    private readonly ICurrentUserService _currentUserService;

    public SimulatorController(
        ISimulatorService simulatorService,
        ICurrentUserService currentUserService)
    {
        _simulatorService = simulatorService;
        _currentUserService = currentUserService;
    }

    private int GetCurrentUserId()
    {
        return _currentUserService.UserId ?? throw new UnauthorizedException("User session is invalid.");
    }

    /// <summary>
    /// Executes a non-destructive, hypothetical investment simulation against an existing portfolio.
    /// Does not alter real transactions, balances, or holdings.
    /// </summary>
    /// <param name="request">Simulation parameters including portfolio, company, transaction type (BUY/SELL), quantity, and hypothetical price.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost]
    [HttpPost("simulate")]
    public async Task<IActionResult> Simulate(
        [FromBody] SimulateTransactionRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _simulatorService.SimulateTransactionAsync(request, userId, cancellationToken);
        return Ok(result);
    }
}
