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
    private readonly ICsvTransactionImportService _csvImportService;
    private readonly ICurrentUserService _currentUserService;

    public TransactionsController(
        ITransactionService transactionService,
        ICsvTransactionImportService csvImportService,
        ICurrentUserService currentUserService)
    {
        _transactionService = transactionService;
        _csvImportService = csvImportService;
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
        var result = await _transactionService.GetPagedTransactionsAsync(userId, filter, cancellationToken);
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
    [HttpGet("available-quantity")]
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

    [HttpGet("import/template")]
    public IActionResult GetImportTemplate()
    {
        var csv = _csvImportService.GenerateSampleCsvTemplate();
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", "sharesync_transactions_template.csv");
    }

    [HttpPost("import/validate")]
    public async Task<IActionResult> ValidateCsv([FromBody] ValidateCsvImportRequestDto request, CancellationToken cancellationToken)
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
        var result = await _csvImportService.ValidateCsvAsync(
            request.PortfolioId,
            request.CsvContent ?? string.Empty,
            userId,
            request.FileName,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("import/upload-validate")]
    public async Task<IActionResult> UploadAndValidateCsv(
        [FromForm] int portfolioId,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (portfolioId <= 0)
        {
            return BadRequest(ApiResponse.Fail("A valid portfolio must be selected."));
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse.Fail("No file uploaded or file is empty."));
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(ApiResponse.Fail("File size exceeds 5 MB limit."));
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".csv" && ext != ".txt")
        {
            return BadRequest(ApiResponse.Fail("Invalid file type. Only .csv or .txt files are supported."));
        }

        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        var csvContent = await reader.ReadToEndAsync(cancellationToken);

        var userId = GetCurrentUserId();
        var result = await _csvImportService.ValidateCsvAsync(
            portfolioId,
            csvContent,
            userId,
            file.FileName,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("import/execute")]
    public async Task<IActionResult> ExecuteCsvImport([FromBody] ExecuteCsvImportRequestDto request, CancellationToken cancellationToken)
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
        var result = await _csvImportService.ExecuteCsvImportAsync(
            request.PortfolioId,
            request.CsvContent,
            request.AllowPartialImport,
            userId,
            cancellationToken);

        return Ok(result);
    }
}
