using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Reports;

namespace ShareSync.Application.Interfaces;

public interface IReportService
{
    Task<ApiResponse<PortfolioHoldingsReportDto>> GetHoldingsReportAsync(
        int userId,
        int? portfolioId = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PortfolioPerformanceReportDto>> GetPerformanceReportAsync(
        int userId,
        int? portfolioId = null,
        string? period = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<TransactionHistoryReportDto>> GetTransactionHistoryReportAsync(
        int userId,
        int? portfolioId = null,
        int? companyId = null,
        string? transactionType = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CompanySectorReportDto>> GetCompanySectorReportAsync(
        int userId,
        int? portfolioId = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<DividendIncomeReportDto>> GetDividendIncomeReportAsync(
        int userId,
        int? companyId = null,
        int? year = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<WatchlistTargetReportDto>> GetWatchlistTargetReportAsync(
        int userId,
        int? watchlistId = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<ReportSummaryDto>> GetReportSummaryAsync(
        int userId,
        int? portfolioId = null,
        CancellationToken cancellationToken = default);
}
