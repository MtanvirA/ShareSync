using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Transactions;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    private readonly ICurrentUserService _currentUserService;

    public TransactionsController(ITransactionService transactionService, ICurrentUserService currentUserService)
    {
        _transactionService = transactionService;
        _currentUserService = currentUserService;
    }

    private int GetCurrentUserId()
    {
        return _currentUserService.UserId ?? throw new UnauthorizedException("User session is invalid.");
    }

    [HttpGet]
    public async Task<IActionResult> GetTransactions([FromQuery] TransactionFilterDto filter, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _transactionService.GetUserTransactionsAsync(userId, filter, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetTransaction(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _transactionService.GetTransactionByIdAsync(id, userId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("available-shares")]
    public async Task<IActionResult> GetAvailableShares([FromQuery] int portfolioId, [FromQuery] int companyId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _transactionService.GetAvailableQuantityAsync(portfolioId, companyId, userId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var userId = GetCurrentUserId();
        var result = await _transactionService.CreateTransactionAsync(request, userId, cancellationToken);
        return CreatedAtAction(nameof(GetTransaction), new { id = result.Data!.TransactionId }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTransaction(int id, [FromBody] UpdateTransactionRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var userId = GetCurrentUserId();
        var result = await _transactionService.UpdateTransactionAsync(id, request, userId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTransaction(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _transactionService.DeleteTransactionAsync(id, userId, cancellationToken);
        return Ok(result);
    }
}
