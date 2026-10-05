using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Transactions;

namespace ShareSync.Application.Interfaces;

public interface ICsvTransactionImportService
{
    Task<ApiResponse<CsvImportPreviewDto>> ValidateCsvAsync(
        int portfolioId,
        string csvContent,
        int userId,
        string? fileName = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CsvImportResultDto>> ExecuteCsvImportAsync(
        int portfolioId,
        string csvContent,
        bool allowPartialImport,
        int userId,
        CancellationToken cancellationToken = default);

    string GenerateSampleCsvTemplate();
}
