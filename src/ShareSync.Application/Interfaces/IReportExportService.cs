using ShareSync.Application.DTOs.Reports;

namespace ShareSync.Application.Interfaces;

public interface IReportExportService
{
    Task<ReportExportResultDto> ExportReportAsync(
        string reportType,
        string format,
        int userId,
        ReportExportFilterDto filter,
        CancellationToken cancellationToken = default);
}
