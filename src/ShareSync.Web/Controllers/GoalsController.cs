using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.DTOs.Goals;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class GoalsController : ControllerBase
{
    private readonly IPortfolioGoalService _goalService;
    private readonly ICurrentUserService _currentUserService;

    public GoalsController(
        IPortfolioGoalService goalService,
        ICurrentUserService currentUserService)
    {
        _goalService = goalService;
        _currentUserService = currentUserService;
    }

    private int GetCurrentUserId()
    {
        return _currentUserService.UserId ?? throw new UnauthorizedException("User session is invalid.");
    }

    /// <summary>
    /// Retrieves all financial goals belonging to the authenticated user, dynamically calculating progress against authoritative portfolio data.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetGoals(
        [FromQuery] int? portfolioId,
        [FromQuery] bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _goalService.GetUserGoalsAsync(userId, portfolioId, activeOnly, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves details and dynamic progress for a specific goal.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetGoalById(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _goalService.GetGoalByIdAsync(id, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new financial goal linked to a portfolio owned by the authenticated user.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateGoal(
        [FromBody] CreatePortfolioGoalRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _goalService.CreateGoalAsync(request, userId, cancellationToken);
        return CreatedAtAction(nameof(GetGoalById), new { id = result.Data?.GoalId }, result);
    }

    /// <summary>
    /// Updates parameters or active status of an existing financial goal.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateGoal(
        int id,
        [FromBody] UpdatePortfolioGoalRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _goalService.UpdateGoalAsync(id, request, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deletes a financial goal belonging to the authenticated user.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteGoal(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _goalService.DeleteGoalAsync(id, userId, cancellationToken);
        return Ok(result);
    }
}
