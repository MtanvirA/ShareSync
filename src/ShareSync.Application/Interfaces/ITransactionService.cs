using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Transactions;

namespace ShareSync.Application.Interfaces;

public interface ITransactionService
{
    Task<ApiResponse<PagedTransactionsDto>> GetPagedTransactionsAsync(int userId, TransactionFilterDto? filter = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<TransactionDto>>> GetUserTransactionsAsync(int userId, TransactionFilterDto? filter = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<TransactionDto>> GetTransactionByIdAsync(int transactionId, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<TransactionDto>> CreateTransactionAsync(CreateTransactionRequestDto request, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<TransactionDto>> UpdateTransactionAsync(int transactionId, UpdateTransactionRequestDto request, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse> DeleteTransactionAsync(int transactionId, int userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<decimal>> GetAvailableQuantityAsync(int portfolioId, int companyId, int userId, CancellationToken cancellationToken = default);
}
