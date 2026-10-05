using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.DTOs.Reports;
using ShareSync.Application.Interfaces;

namespace ShareSync.Application.Services;

public class ReportExportService : IReportExportService
{
    static ReportExportService()
    {
        // QuestPDF license configuration
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;
                QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
    }

    private readonly IReportService _reportService;
    private readonly IApplicationDbContext _context;

    public ReportExportService(IReportService reportService, IApplicationDbContext context)
    {
        _reportService = reportService;
        _context = context;
    }

    public async Task<ReportExportResultDto> ExportReportAsync(
        string reportType,
        string format,
        int userId,
        ReportExportFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var normType = (reportType ?? string.Empty).Trim().ToLowerInvariant();
        var normFormat = (format ?? string.Empty).Trim().ToLowerInvariant();

        if (normFormat is "xlsx") normFormat = "excel";

        if (normFormat != "pdf" && normFormat != "excel" && normFormat != "csv")
        {
            throw new AppException($"Unsupported export format '{format}'. Supported formats are PDF, Excel, and CSV.", 400);
        }

        // Fetch context info (user and optional portfolio name)
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        var userName = user?.Name ?? $"User #{userId}";

        string? portfolioName = null;
        if (filter.PortfolioId.HasValue)
        {
            var p = await _context.Portfolios.AsNoTracking().FirstOrDefaultAsync(x => x.PortfolioId == filter.PortfolioId.Value, cancellationToken);
            if (p != null) portfolioName = p.PortfolioName;
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        return normType switch
        {
            "holdings" => await ExportHoldingsAsync(normFormat, userId, userName, portfolioName, filter, timestamp, cancellationToken),
            "performance" => await ExportPerformanceAsync(normFormat, userId, userName, portfolioName, filter, timestamp, cancellationToken),
            "transactions" => await ExportTransactionsAsync(normFormat, userId, userName, portfolioName, filter, timestamp, cancellationToken),
            "company-sector" or "sector" => await ExportCompanySectorAsync(normFormat, userId, userName, portfolioName, filter, timestamp, cancellationToken),
            "dividends" or "dividend" => await ExportDividendsAsync(normFormat, userId, userName, portfolioName, filter, timestamp, cancellationToken),
            "watchlist" => await ExportWatchlistAsync(normFormat, userId, userName, filter, timestamp, cancellationToken),
            _ => throw new AppException($"Unknown report type '{reportType}'. Supported reports: holdings, performance, transactions, company-sector, dividends, watchlist.", 400)
        };
    }

    #region 1. Portfolio Holdings Export

    private async Task<ReportExportResultDto> ExportHoldingsAsync(
        string format, int userId, string userName, string? portfolioName,
        ReportExportFilterDto filter, string timestamp, CancellationToken ct)
    {
        var res = await _reportService.GetHoldingsReportAsync(userId, filter.PortfolioId, ct);
        var report = res.Data ?? new PortfolioHoldingsReportDto();
        var fileBase = $"sharesync_holdings_{timestamp}";

        return format switch
        {
            "csv" => CreateCsvResult(BuildHoldingsCsv(report, userName, portfolioName), $"{fileBase}.csv"),
            "excel" => CreateExcelResult(BuildHoldingsExcel(report, userName, portfolioName), $"{fileBase}.xlsx"),
            "pdf" => CreatePdfResult(BuildHoldingsPdf(report, userName, portfolioName), $"{fileBase}.pdf"),
            _ => throw new AppException("Invalid format", 400)
        };
    }

    private static string BuildHoldingsCsv(PortfolioHoldingsReportDto r, string user, string? portfolio)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ShareSync - Portfolio Holdings Report");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC,User: {EscapeCsv(user)},Portfolio: {EscapeCsv(portfolio ?? "All Portfolios")}");
        sb.AppendLine($"Total Invested: {r.TotalInvested:F2},Total Market Value: {r.TotalMarketValue:F2},Total Unrealized P/L: {r.TotalUnrealizedProfitLoss:F2},Overall Return: {r.TotalProfitLossPercentage:F2}%");
        sb.AppendLine();
        sb.AppendLine("Ticker,Company Name,Portfolio,Current Quantity,Weighted Avg Buy Price,Current Market Price,Invested Value,Current Market Value,Unrealized P/L,P/L %");

        foreach (var h in r.Holdings)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(h.TickerSymbol),
                EscapeCsv(h.CompanyName),
                EscapeCsv(h.PortfolioName),
                h.CurrentQuantity.ToString("F4", CultureInfo.InvariantCulture),
                h.WeightedAverageBuyPrice.ToString("F2", CultureInfo.InvariantCulture),
                h.CurrentMarketPrice.ToString("F2", CultureInfo.InvariantCulture),
                h.InvestedValue.ToString("F2", CultureInfo.InvariantCulture),
                h.CurrentMarketValue.ToString("F2", CultureInfo.InvariantCulture),
                h.UnrealizedProfitLoss.ToString("F2", CultureInfo.InvariantCulture),
                h.ProfitLossPercentage.ToString("F2", CultureInfo.InvariantCulture) + "%"
            ));
        }

        sb.AppendLine(string.Join(",",
            "TOTAL",
            $"\"{r.HoldingsCount} Holdings\"",
            "-",
            "-",
            "-",
            "-",
            r.TotalInvested.ToString("F2", CultureInfo.InvariantCulture),
            r.TotalMarketValue.ToString("F2", CultureInfo.InvariantCulture),
            r.TotalUnrealizedProfitLoss.ToString("F2", CultureInfo.InvariantCulture),
            r.TotalProfitLossPercentage.ToString("F2", CultureInfo.InvariantCulture) + "%"
        ));

        return sb.ToString();
    }

    private static byte[] BuildHoldingsExcel(PortfolioHoldingsReportDto r, string user, string? portfolio)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Portfolio Holdings");

        // Header Title Block
        ws.Cell(1, 1).Value = "ShareSync - Portfolio Holdings Report";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#0F172A");

        ws.Cell(2, 1).Value = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC | Account: {user} | Portfolio: {portfolio ?? "All Portfolios"}";
        ws.Cell(2, 1).Style.Font.Italic = true;
        ws.Cell(2, 1).Style.Font.FontSize = 9;

        // Metric summary row
        ws.Cell(4, 1).Value = "Total Invested";
        ws.Cell(4, 2).Value = (double)r.TotalInvested;
        ws.Cell(4, 2).Style.NumberFormat.Format = "৳#,##0.00";

        ws.Cell(4, 3).Value = "Market Value";
        ws.Cell(4, 4).Value = (double)r.TotalMarketValue;
        ws.Cell(4, 4).Style.NumberFormat.Format = "৳#,##0.00";

        ws.Cell(4, 5).Value = "Unrealized P/L";
        ws.Cell(4, 6).Value = (double)r.TotalUnrealizedProfitLoss;
        ws.Cell(4, 6).Style.NumberFormat.Format = "৳#,##0.00";

        ws.Cell(4, 7).Value = "Total Return";
        ws.Cell(4, 8).Value = (double)(r.TotalProfitLossPercentage / 100m);
        ws.Cell(4, 8).Style.NumberFormat.Format = "0.00%";

        ws.Range(4, 1, 4, 8).Style.Font.Bold = true;
        ws.Range(4, 1, 4, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");

        // Table Header
        int row = 6;
        string[] headers = { "Ticker", "Company Name", "Portfolio", "Quantity", "Avg Buy Price", "Current Price", "Invested Value", "Market Value", "Unrealized P/L", "P/L %" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F172A");
            cell.Style.Alignment.Horizontal = i >= 3 ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
        }

        row++;
        foreach (var h in r.Holdings)
        {
            ws.Cell(row, 1).Value = h.TickerSymbol;
            ws.Cell(row, 2).Value = h.CompanyName;
            ws.Cell(row, 3).Value = h.PortfolioName;

            ws.Cell(row, 4).Value = (double)h.CurrentQuantity;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.0000";

            ws.Cell(row, 5).Value = (double)h.WeightedAverageBuyPrice;
            ws.Cell(row, 5).Style.NumberFormat.Format = "৳#,##0.00";

            ws.Cell(row, 6).Value = (double)h.CurrentMarketPrice;
            ws.Cell(row, 6).Style.NumberFormat.Format = "৳#,##0.00";

            ws.Cell(row, 7).Value = (double)h.InvestedValue;
            ws.Cell(row, 7).Style.NumberFormat.Format = "৳#,##0.00";

            ws.Cell(row, 8).Value = (double)h.CurrentMarketValue;
            ws.Cell(row, 8).Style.NumberFormat.Format = "৳#,##0.00";

            ws.Cell(row, 9).Value = (double)h.UnrealizedProfitLoss;
            ws.Cell(row, 9).Style.NumberFormat.Format = "৳#,##0.00";
            if (h.UnrealizedProfitLoss >= 0)
                ws.Cell(row, 9).Style.Font.FontColor = XLColor.FromHtml("#167A4A");
            else
                ws.Cell(row, 9).Style.Font.FontColor = XLColor.FromHtml("#C0392B");

            ws.Cell(row, 10).Value = (double)(h.ProfitLossPercentage / 100m);
            ws.Cell(row, 10).Style.NumberFormat.Format = "0.00%";
            if (h.ProfitLossPercentage >= 0)
                ws.Cell(row, 10).Style.Font.FontColor = XLColor.FromHtml("#167A4A");
            else
                ws.Cell(row, 10).Style.Font.FontColor = XLColor.FromHtml("#C0392B");

            if (row % 2 == 1)
            {
                ws.Range(row, 1, row, 10).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
            }
            row++;
        }

        // Totals row
        ws.Cell(row, 1).Value = "TOTAL";
        ws.Cell(row, 2).Value = $"{r.HoldingsCount} Holdings";
        ws.Cell(row, 7).Value = (double)r.TotalInvested;
        ws.Cell(row, 7).Style.NumberFormat.Format = "৳#,##0.00";
        ws.Cell(row, 8).Value = (double)r.TotalMarketValue;
        ws.Cell(row, 8).Style.NumberFormat.Format = "৳#,##0.00";
        ws.Cell(row, 9).Value = (double)r.TotalUnrealizedProfitLoss;
        ws.Cell(row, 9).Style.NumberFormat.Format = "৳#,##0.00";
        ws.Cell(row, 10).Value = (double)(r.TotalProfitLossPercentage / 100m);
        ws.Cell(row, 10).Style.NumberFormat.Format = "0.00%";

        var totalRange = ws.Range(row, 1, row, 10);
        totalRange.Style.Font.Bold = true;
        totalRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
        totalRange.Style.Border.BottomBorder = XLBorderStyleValues.Double;
        totalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] BuildHoldingsPdf(PortfolioHoldingsReportDto r, string user, string? portfolio)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Lato"));

                page.Header().Element(c => ComposeHeader(c, "PORTFOLIO HOLDINGS REPORT", user, portfolio));

                page.Content().Element(c =>
                {
                    c.Column(col =>
                    {
                        // Summary Metrics Bar
                        col.Item().PaddingBottom(12).Row(rRow =>
                        {
                            rRow.Spacing(10);
                            rRow.RelativeItem().Element(e => StatBox(e, "Total Invested", $"BDT {r.TotalInvested:N2}"));
                            rRow.RelativeItem().Element(e => StatBox(e, "Market Value", $"BDT {r.TotalMarketValue:N2}"));
                            rRow.RelativeItem().Element(e => StatBox(e, "Unrealized P/L", $"BDT {r.TotalUnrealizedProfitLoss:N2}", r.TotalUnrealizedProfitLoss >= 0));
                            rRow.RelativeItem().Element(e => StatBox(e, "Overall Return", $"{r.TotalProfitLossPercentage:N2}%", r.TotalProfitLossPercentage >= 0));
                        });

                        // Table
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(65);  // Ticker
                                columns.RelativeColumn(3);   // Company
                                columns.RelativeColumn(2);   // Portfolio
                                columns.RelativeColumn(1.5f);// Qty
                                columns.RelativeColumn(1.5f);// Buy Price
                                columns.RelativeColumn(1.5f);// Mkt Price
                                columns.RelativeColumn(2);   // Invested
                                columns.RelativeColumn(2);   // Value
                                columns.RelativeColumn(2);   // P/L
                                columns.RelativeColumn(1.5f);// P/L %
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderStyle).Text("Ticker");
                                h.Cell().Element(HeaderStyle).Text("Company");
                                h.Cell().Element(HeaderStyle).Text("Portfolio");
                                h.Cell().Element(HeaderRightStyle).Text("Quantity");
                                h.Cell().Element(HeaderRightStyle).Text("Avg Price");
                                h.Cell().Element(HeaderRightStyle).Text("Price");
                                h.Cell().Element(HeaderRightStyle).Text("Invested");
                                h.Cell().Element(HeaderRightStyle).Text("Market Value");
                                h.Cell().Element(HeaderRightStyle).Text("Unrealized P/L");
                                h.Cell().Element(HeaderRightStyle).Text("P/L %");
                            });

                            if (r.Holdings.Count == 0)
                            {
                                table.Cell().ColumnSpan(10).Padding(16).AlignCenter().Text("No holdings found matching criteria.").Italic().FontColor(Colors.Grey.Medium);
                            }
                            else
                            {
                                bool alt = false;
                                foreach (var item in r.Holdings)
                                {
                                    var bg = alt ? Colors.Grey.Lighten4 : Colors.White;
                                    table.Cell().Background(bg).Padding(5).Text(item.TickerSymbol).Bold();
                                    table.Cell().Background(bg).Padding(5).Text(item.CompanyName);
                                    table.Cell().Background(bg).Padding(5).Text(item.PortfolioName);
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"{item.CurrentQuantity:N2}");
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"BDT {item.WeightedAverageBuyPrice:N2}");
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"BDT {item.CurrentMarketPrice:N2}");
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"BDT {item.InvestedValue:N2}");
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"BDT {item.CurrentMarketValue:N2}");

                                    var plColor = item.UnrealizedProfitLoss >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2;
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"BDT {item.UnrealizedProfitLoss:N2}").FontColor(plColor);
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"{item.ProfitLossPercentage:N2}%").FontColor(plColor);

                                    alt = !alt;
                                }

                                // Totals Row
                                table.Cell().ColumnSpan(3).Element(TotalStyle).Text($"TOTAL ({r.HoldingsCount} Holdings)");
                                table.Cell().ColumnSpan(3).Element(TotalStyle).Text("");
                                table.Cell().Element(TotalRightStyle).Text($"BDT {r.TotalInvested:N2}");
                                table.Cell().Element(TotalRightStyle).Text($"BDT {r.TotalMarketValue:N2}");
                                var totalColor = r.TotalUnrealizedProfitLoss >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2;
                                table.Cell().Element(TotalRightStyle).Text($"BDT {r.TotalUnrealizedProfitLoss:N2}").FontColor(totalColor);
                                table.Cell().Element(TotalRightStyle).Text($"{r.TotalProfitLossPercentage:N2}%").FontColor(totalColor);
                            }
                        });
                    });
                });

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    #endregion

    #region 2. Portfolio Performance Export

    private async Task<ReportExportResultDto> ExportPerformanceAsync(
        string format, int userId, string userName, string? portfolioName,
        ReportExportFilterDto filter, string timestamp, CancellationToken ct)
    {
        var res = await _reportService.GetPerformanceReportAsync(userId, filter.PortfolioId, filter.Period, ct);
        var report = res.Data ?? new PortfolioPerformanceReportDto();
        var fileBase = $"sharesync_performance_{timestamp}";

        return format switch
        {
            "csv" => CreateCsvResult(BuildPerformanceCsv(report, userName, portfolioName, filter.Period), $"{fileBase}.csv"),
            "excel" => CreateExcelResult(BuildPerformanceExcel(report, userName, portfolioName, filter.Period), $"{fileBase}.xlsx"),
            "pdf" => CreatePdfResult(BuildPerformancePdf(report, userName, portfolioName, filter.Period), $"{fileBase}.pdf"),
            _ => throw new AppException("Invalid format", 400)
        };
    }

    private static string BuildPerformanceCsv(PortfolioPerformanceReportDto r, string user, string? portfolio, string? period)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ShareSync - Portfolio Performance Report");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC,User: {EscapeCsv(user)},Portfolio: {EscapeCsv(portfolio ?? "All Portfolios")},Period: {EscapeCsv(period ?? "All time")}");
        sb.AppendLine($"Starting Value: {r.StartingValue:F2},Current Value: {r.CurrentValue:F2},Net Change: {r.NetChange:F2},Net Change %: {r.NetChangePercentage:F2}%");
        sb.AppendLine();
        sb.AppendLine("Date,Portfolio Value,Change From Previous,Percentage Change");

        foreach (var s in r.Snapshots)
        {
            sb.AppendLine(string.Join(",",
                s.SnapshotDate.ToString("yyyy-MM-dd"),
                s.PortfolioValue.ToString("F2", CultureInfo.InvariantCulture),
                s.ChangeFromPrevious.HasValue ? s.ChangeFromPrevious.Value.ToString("F2", CultureInfo.InvariantCulture) : "-",
                s.PercentageChange.HasValue ? s.PercentageChange.Value.ToString("F2", CultureInfo.InvariantCulture) + "%" : "-"
            ));
        }

        return sb.ToString();
    }

    private static byte[] BuildPerformanceExcel(PortfolioPerformanceReportDto r, string user, string? portfolio, string? period)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Portfolio Performance");

        ws.Cell(1, 1).Value = "ShareSync - Portfolio Performance / P&L Report";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC | Account: {user} | Portfolio: {portfolio ?? "All Portfolios"} | Period: {period ?? "All time"}";
        ws.Cell(2, 1).Style.Font.Italic = true;

        ws.Cell(4, 1).Value = "Starting Value";
        ws.Cell(4, 2).Value = (double)r.StartingValue;
        ws.Cell(4, 2).Style.NumberFormat.Format = "৳#,##0.00";

        ws.Cell(4, 3).Value = "Current Value";
        ws.Cell(4, 4).Value = (double)r.CurrentValue;
        ws.Cell(4, 4).Style.NumberFormat.Format = "৳#,##0.00";

        ws.Cell(4, 5).Value = "Net Change";
        ws.Cell(4, 6).Value = (double)r.NetChange;
        ws.Cell(4, 6).Style.NumberFormat.Format = "৳#,##0.00";

        ws.Cell(4, 7).Value = "Change %";
        ws.Cell(4, 8).Value = (double)(r.NetChangePercentage / 100m);
        ws.Cell(4, 8).Style.NumberFormat.Format = "0.00%";
        ws.Range(4, 1, 4, 8).Style.Font.Bold = true;
        ws.Range(4, 1, 4, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");

        int row = 6;
        string[] headers = { "Date", "Portfolio Value", "Change From Previous", "Percentage Change" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F172A");
            cell.Style.Alignment.Horizontal = i >= 1 ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
        }

        row++;
        foreach (var s in r.Snapshots)
        {
            ws.Cell(row, 1).Value = s.SnapshotDate.ToString("yyyy-MM-dd");
            ws.Cell(row, 2).Value = (double)s.PortfolioValue;
            ws.Cell(row, 2).Style.NumberFormat.Format = "৳#,##0.00";

            if (s.ChangeFromPrevious.HasValue)
            {
                ws.Cell(row, 3).Value = (double)s.ChangeFromPrevious.Value;
                ws.Cell(row, 3).Style.NumberFormat.Format = "৳#,##0.00";
            }
            else ws.Cell(row, 3).Value = "-";

            if (s.PercentageChange.HasValue)
            {
                ws.Cell(row, 4).Value = (double)(s.PercentageChange.Value / 100m);
                ws.Cell(row, 4).Style.NumberFormat.Format = "0.00%";
            }
            else ws.Cell(row, 4).Value = "-";

            row++;
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] BuildPerformancePdf(PortfolioPerformanceReportDto r, string user, string? portfolio, string? period)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Lato"));

                page.Header().Element(c => ComposeHeader(c, "PORTFOLIO PERFORMANCE REPORT", user, portfolio, $"Period: {period ?? "All time"}"));

                page.Content().Element(c =>
                {
                    c.Column(col =>
                    {
                        col.Item().PaddingBottom(12).Row(rRow =>
                        {
                            rRow.Spacing(10);
                            rRow.RelativeItem().Element(e => StatBox(e, "Starting Value", $"BDT {r.StartingValue:N2}"));
                            rRow.RelativeItem().Element(e => StatBox(e, "Current Value", $"BDT {r.CurrentValue:N2}"));
                            rRow.RelativeItem().Element(e => StatBox(e, "Net Change", $"BDT {r.NetChange:N2}", r.NetChange >= 0));
                            rRow.RelativeItem().Element(e => StatBox(e, "Return %", $"{r.NetChangePercentage:N2}%", r.NetChangePercentage >= 0));
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderStyle).Text("Date");
                                h.Cell().Element(HeaderRightStyle).Text("Portfolio Value");
                                h.Cell().Element(HeaderRightStyle).Text("Change");
                                h.Cell().Element(HeaderRightStyle).Text("Change %");
                            });

                            if (r.Snapshots.Count == 0)
                            {
                                table.Cell().ColumnSpan(4).Padding(16).AlignCenter().Text("No performance snapshot records found.").Italic().FontColor(Colors.Grey.Medium);
                            }
                            else
                            {
                                bool alt = false;
                                foreach (var s in r.Snapshots)
                                {
                                    var bg = alt ? Colors.Grey.Lighten4 : Colors.White;
                                    table.Cell().Background(bg).Padding(5).Text(s.SnapshotDate.ToString("yyyy-MM-dd"));
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"BDT {s.PortfolioValue:N2}");
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text(s.ChangeFromPrevious.HasValue ? $"BDT {s.ChangeFromPrevious.Value:N2}" : "-");
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text(s.PercentageChange.HasValue ? $"{s.PercentageChange.Value:N2}%" : "-");
                                    alt = !alt;
                                }
                            }
                        });
                    });
                });

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    #endregion

    #region 3. Transaction History Export

    private async Task<ReportExportResultDto> ExportTransactionsAsync(
        string format, int userId, string userName, string? portfolioName,
        ReportExportFilterDto filter, string timestamp, CancellationToken ct)
    {
        var res = await _reportService.GetTransactionHistoryReportAsync(
            userId, filter.PortfolioId, filter.CompanyId, filter.TransactionType, filter.StartDate, filter.EndDate, ct);
        var report = res.Data ?? new TransactionHistoryReportDto();
        var fileBase = $"sharesync_transactions_{timestamp}";

        return format switch
        {
            "csv" => CreateCsvResult(BuildTransactionsCsv(report, userName, portfolioName, filter), $"{fileBase}.csv"),
            "excel" => CreateExcelResult(BuildTransactionsExcel(report, userName, portfolioName, filter), $"{fileBase}.xlsx"),
            "pdf" => CreatePdfResult(BuildTransactionsPdf(report, userName, portfolioName, filter), $"{fileBase}.pdf"),
            _ => throw new AppException("Invalid format", 400)
        };
    }

    private static string BuildTransactionsCsv(TransactionHistoryReportDto r, string user, string? portfolio, ReportExportFilterDto f)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ShareSync - Transaction History Report");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC,User: {EscapeCsv(user)},Portfolio: {EscapeCsv(portfolio ?? "All Portfolios")},Type: {EscapeCsv(f.TransactionType ?? "All")}");
        sb.AppendLine($"Total Transactions: {r.TotalTransactions},Total Buy Value: {r.TotalBuyValue:F2},Total Sell Value: {r.TotalSellValue:F2},Net Cash Flow: {r.NetCashFlow:F2}");
        sb.AppendLine();
        sb.AppendLine("Date,Ticker,Company Name,Type,Quantity,Price Per Share,Total Value,Portfolio");

        foreach (var t in r.Transactions)
        {
            sb.AppendLine(string.Join(",",
                t.TransactionDate.ToString("yyyy-MM-dd"),
                EscapeCsv(t.TickerSymbol),
                EscapeCsv(t.CompanyName),
                EscapeCsv(t.TransactionType),
                t.Quantity.ToString("F4", CultureInfo.InvariantCulture),
                t.PricePerShare.ToString("F2", CultureInfo.InvariantCulture),
                t.TotalTransactionValue.ToString("F2", CultureInfo.InvariantCulture),
                EscapeCsv(t.PortfolioName)
            ));
        }

        sb.AppendLine(string.Join(",",
            "TOTAL",
            "-",
            $"{r.TotalTransactions} Trades",
            $"BUY: {r.BuyCount} | SELL: {r.SellCount}",
            "-",
            "-",
            r.NetCashFlow.ToString("F2", CultureInfo.InvariantCulture),
            "-"
        ));

        return sb.ToString();
    }

    private static byte[] BuildTransactionsExcel(TransactionHistoryReportDto r, string user, string? portfolio, ReportExportFilterDto f)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Transaction History");

        ws.Cell(1, 1).Value = "ShareSync - Transaction History Report";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC | Account: {user} | Portfolio: {portfolio ?? "All Portfolios"} | Type: {f.TransactionType ?? "All"}";
        ws.Cell(2, 1).Style.Font.Italic = true;

        ws.Cell(4, 1).Value = "Total Trades";
        ws.Cell(4, 2).Value = r.TotalTransactions;

        ws.Cell(4, 3).Value = "Total Buy Value";
        ws.Cell(4, 4).Value = (double)r.TotalBuyValue;
        ws.Cell(4, 4).Style.NumberFormat.Format = "৳#,##0.00";

        ws.Cell(4, 5).Value = "Total Sell Value";
        ws.Cell(4, 6).Value = (double)r.TotalSellValue;
        ws.Cell(4, 6).Style.NumberFormat.Format = "৳#,##0.00";

        ws.Cell(4, 7).Value = "Net Cash Flow";
        ws.Cell(4, 8).Value = (double)r.NetCashFlow;
        ws.Cell(4, 8).Style.NumberFormat.Format = "৳#,##0.00";

        ws.Range(4, 1, 4, 8).Style.Font.Bold = true;
        ws.Range(4, 1, 4, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");

        int row = 6;
        string[] headers = { "Date", "Ticker", "Company Name", "Type", "Quantity", "Price", "Total Value", "Portfolio" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F172A");
            cell.Style.Alignment.Horizontal = (i == 4 || i == 5 || i == 6) ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
        }

        row++;
        foreach (var t in r.Transactions)
        {
            ws.Cell(row, 1).Value = t.TransactionDate.ToString("yyyy-MM-dd");
            ws.Cell(row, 2).Value = t.TickerSymbol;
            ws.Cell(row, 3).Value = t.CompanyName;
            ws.Cell(row, 4).Value = t.TransactionType;

            ws.Cell(row, 5).Value = (double)t.Quantity;
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.0000";

            ws.Cell(row, 6).Value = (double)t.PricePerShare;
            ws.Cell(row, 6).Style.NumberFormat.Format = "৳#,##0.00";

            ws.Cell(row, 7).Value = (double)t.TotalTransactionValue;
            ws.Cell(row, 7).Style.NumberFormat.Format = "৳#,##0.00";

            ws.Cell(row, 8).Value = t.PortfolioName;
            row++;
        }

        ws.Cell(row, 1).Value = "TOTAL";
        ws.Cell(row, 3).Value = $"{r.TotalTransactions} Trades";
        ws.Cell(row, 7).Value = (double)r.NetCashFlow;
        ws.Cell(row, 7).Style.NumberFormat.Format = "৳#,##0.00";
        ws.Range(row, 1, row, 8).Style.Font.Bold = true;
        ws.Range(row, 1, row, 8).Style.Border.TopBorder = XLBorderStyleValues.Thin;
        ws.Range(row, 1, row, 8).Style.Border.BottomBorder = XLBorderStyleValues.Double;

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] BuildTransactionsPdf(TransactionHistoryReportDto r, string user, string? portfolio, ReportExportFilterDto f)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Lato"));

                page.Header().Element(c => ComposeHeader(c, "TRANSACTION HISTORY REPORT", user, portfolio, $"Type: {f.TransactionType ?? "All"}"));

                page.Content().Element(c =>
                {
                    c.Column(col =>
                    {
                        col.Item().PaddingBottom(12).Row(rRow =>
                        {
                            rRow.Spacing(10);
                            rRow.RelativeItem().Element(e => StatBox(e, "Total Trades", r.TotalTransactions.ToString()));
                            rRow.RelativeItem().Element(e => StatBox(e, "Total Buy Value", $"BDT {r.TotalBuyValue:N2}"));
                            rRow.RelativeItem().Element(e => StatBox(e, "Total Sell Value", $"BDT {r.TotalSellValue:N2}"));
                            rRow.RelativeItem().Element(e => StatBox(e, "Net Cash Flow", $"BDT {r.NetCashFlow:N2}"));
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(70);  // Date
                                columns.ConstantColumn(65);  // Ticker
                                columns.RelativeColumn(3);   // Company
                                columns.ConstantColumn(50);  // Type
                                columns.RelativeColumn(1.5f);// Qty
                                columns.RelativeColumn(1.5f);// Price
                                columns.RelativeColumn(2);   // Total
                                columns.RelativeColumn(2);   // Portfolio
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderStyle).Text("Date");
                                h.Cell().Element(HeaderStyle).Text("Ticker");
                                h.Cell().Element(HeaderStyle).Text("Company");
                                h.Cell().Element(HeaderStyle).Text("Type");
                                h.Cell().Element(HeaderRightStyle).Text("Quantity");
                                h.Cell().Element(HeaderRightStyle).Text("Price");
                                h.Cell().Element(HeaderRightStyle).Text("Total Value");
                                h.Cell().Element(HeaderStyle).Text("Portfolio");
                            });

                            if (r.Transactions.Count == 0)
                            {
                                table.Cell().ColumnSpan(8).Padding(16).AlignCenter().Text("No transactions found matching criteria.").Italic().FontColor(Colors.Grey.Medium);
                            }
                            else
                            {
                                bool alt = false;
                                foreach (var t in r.Transactions)
                                {
                                    var bg = alt ? Colors.Grey.Lighten4 : Colors.White;
                                    table.Cell().Background(bg).Padding(5).Text(t.TransactionDate.ToString("yyyy-MM-dd"));
                                    table.Cell().Background(bg).Padding(5).Text(t.TickerSymbol).Bold();
                                    table.Cell().Background(bg).Padding(5).Text(t.CompanyName);

                                    var typeColor = t.TransactionType == "BUY" ? Colors.Blue.Darken2 : Colors.Green.Darken2;
                                    table.Cell().Background(bg).Padding(5).Text(t.TransactionType).Bold().FontColor(typeColor);

                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"{t.Quantity:N2}");
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"BDT {t.PricePerShare:N2}");
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"BDT {t.TotalTransactionValue:N2}");
                                    table.Cell().Background(bg).Padding(5).Text(t.PortfolioName);

                                    alt = !alt;
                                }

                                table.Cell().ColumnSpan(4).Element(TotalStyle).Text($"TOTAL ({r.TotalTransactions} Trades)");
                                table.Cell().ColumnSpan(2).Element(TotalStyle).Text("");
                                table.Cell().Element(TotalRightStyle).Text($"BDT {r.NetCashFlow:N2}");
                                table.Cell().Element(TotalStyle).Text("");
                            }
                        });
                    });
                });

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    #endregion

    #region 4. Company & Sector Allocation Export

    private async Task<ReportExportResultDto> ExportCompanySectorAsync(
        string format, int userId, string userName, string? portfolioName,
        ReportExportFilterDto filter, string timestamp, CancellationToken ct)
    {
        var res = await _reportService.GetCompanySectorReportAsync(userId, filter.PortfolioId, ct);
        var report = res.Data ?? new CompanySectorReportDto();
        var fileBase = $"sharesync_company_sector_{timestamp}";

        return format switch
        {
            "csv" => CreateCsvResult(BuildCompanySectorCsv(report, userName, portfolioName), $"{fileBase}.csv"),
            "excel" => CreateExcelResult(BuildCompanySectorExcel(report, userName, portfolioName), $"{fileBase}.xlsx"),
            "pdf" => CreatePdfResult(BuildCompanySectorPdf(report, userName, portfolioName), $"{fileBase}.pdf"),
            _ => throw new AppException("Invalid format", 400)
        };
    }

    private static string BuildCompanySectorCsv(CompanySectorReportDto r, string user, string? portfolio)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ShareSync - Company & Sector Investment Report");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC,User: {EscapeCsv(user)},Portfolio: {EscapeCsv(portfolio ?? "All Portfolios")}");
        sb.AppendLine($"Total Portfolio Value: {r.TotalPortfolioValue:F2}");
        sb.AppendLine();
        sb.AppendLine("--- SECTOR ALLOCATION ---");
        sb.AppendLine("Sector Name,Holdings Count,Total Invested,Current Value,Unrealized P/L,Allocation %");
        foreach (var s in r.Sectors)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(s.SectorName),
                s.HoldingsCount,
                s.TotalInvested.ToString("F2", CultureInfo.InvariantCulture),
                s.CurrentMarketValue.ToString("F2", CultureInfo.InvariantCulture),
                s.UnrealizedProfitLoss.ToString("F2", CultureInfo.InvariantCulture),
                s.AllocationPercentage.ToString("F2", CultureInfo.InvariantCulture) + "%"
            ));
        }

        sb.AppendLine();
        sb.AppendLine("--- COMPANY HOLDINGS ---");
        sb.AppendLine("Ticker,Company Name,Sector,Shares Held,Total Invested,Current Value,Unrealized P/L,Allocation %");
        foreach (var c in r.Companies)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(c.TickerSymbol),
                EscapeCsv(c.CompanyName),
                EscapeCsv(c.SectorName),
                c.SharesHeld.ToString("F4", CultureInfo.InvariantCulture),
                c.TotalInvested.ToString("F2", CultureInfo.InvariantCulture),
                c.CurrentMarketValue.ToString("F2", CultureInfo.InvariantCulture),
                c.UnrealizedProfitLoss.ToString("F2", CultureInfo.InvariantCulture),
                c.AllocationPercentage.ToString("F2", CultureInfo.InvariantCulture) + "%"
            ));
        }

        return sb.ToString();
    }

    private static byte[] BuildCompanySectorExcel(CompanySectorReportDto r, string user, string? portfolio)
    {
        using var wb = new XLWorkbook();

        // Sector Sheet
        var wsSec = wb.Worksheets.Add("Sector Allocation");
        wsSec.Cell(1, 1).Value = "ShareSync - Sector Allocation Report";
        wsSec.Cell(1, 1).Style.Font.Bold = true;
        wsSec.Cell(1, 1).Style.Font.FontSize = 14;

        wsSec.Cell(2, 1).Value = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC | Account: {user} | Portfolio: {portfolio ?? "All Portfolios"} | Value: ৳{r.TotalPortfolioValue:N2}";
        wsSec.Cell(2, 1).Style.Font.Italic = true;

        int row = 4;
        string[] secHeaders = { "Sector Name", "Holdings", "Total Invested", "Market Value", "Unrealized P/L", "Allocation %" };
        for (int i = 0; i < secHeaders.Length; i++)
        {
            var cell = wsSec.Cell(row, i + 1);
            cell.Value = secHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F172A");
            cell.Style.Alignment.Horizontal = i >= 1 ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
        }

        row++;
        foreach (var s in r.Sectors)
        {
            wsSec.Cell(row, 1).Value = s.SectorName;
            wsSec.Cell(row, 2).Value = s.HoldingsCount;
            wsSec.Cell(row, 3).Value = (double)s.TotalInvested;
            wsSec.Cell(row, 3).Style.NumberFormat.Format = "৳#,##0.00";
            wsSec.Cell(row, 4).Value = (double)s.CurrentMarketValue;
            wsSec.Cell(row, 4).Style.NumberFormat.Format = "৳#,##0.00";
            wsSec.Cell(row, 5).Value = (double)s.UnrealizedProfitLoss;
            wsSec.Cell(row, 5).Style.NumberFormat.Format = "৳#,##0.00";
            wsSec.Cell(row, 6).Value = (double)(s.AllocationPercentage / 100m);
            wsSec.Cell(row, 6).Style.NumberFormat.Format = "0.00%";
            row++;
        }
        wsSec.Columns().AdjustToContents();

        // Company Sheet
        var wsComp = wb.Worksheets.Add("Company Investment");
        wsComp.Cell(1, 1).Value = "ShareSync - Company Investment Breakdown";
        wsComp.Cell(1, 1).Style.Font.Bold = true;
        wsComp.Cell(1, 1).Style.Font.FontSize = 14;

        row = 4;
        string[] compHeaders = { "Ticker", "Company Name", "Sector", "Shares Held", "Total Invested", "Market Value", "Unrealized P/L", "Allocation %" };
        for (int i = 0; i < compHeaders.Length; i++)
        {
            var cell = wsComp.Cell(row, i + 1);
            cell.Value = compHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F172A");
            cell.Style.Alignment.Horizontal = i >= 3 ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
        }

        row++;
        foreach (var c in r.Companies)
        {
            wsComp.Cell(row, 1).Value = c.TickerSymbol;
            wsComp.Cell(row, 2).Value = c.CompanyName;
            wsComp.Cell(row, 3).Value = c.SectorName;
            wsComp.Cell(row, 4).Value = (double)c.SharesHeld;
            wsComp.Cell(row, 4).Style.NumberFormat.Format = "#,##0.0000";
            wsComp.Cell(row, 5).Value = (double)c.TotalInvested;
            wsComp.Cell(row, 5).Style.NumberFormat.Format = "৳#,##0.00";
            wsComp.Cell(row, 6).Value = (double)c.CurrentMarketValue;
            wsComp.Cell(row, 6).Style.NumberFormat.Format = "৳#,##0.00";
            wsComp.Cell(row, 7).Value = (double)c.UnrealizedProfitLoss;
            wsComp.Cell(row, 7).Style.NumberFormat.Format = "৳#,##0.00";
            wsComp.Cell(row, 8).Value = (double)(c.AllocationPercentage / 100m);
            wsComp.Cell(row, 8).Style.NumberFormat.Format = "0.00%";
            row++;
        }
        wsComp.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] BuildCompanySectorPdf(CompanySectorReportDto r, string user, string? portfolio)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Lato"));

                page.Header().Element(c => ComposeHeader(c, "COMPANY & SECTOR INVESTMENT REPORT", user, portfolio));

                page.Content().Element(c =>
                {
                    c.Column(col =>
                    {
                        col.Item().PaddingBottom(12).Row(rRow =>
                        {
                            rRow.Spacing(10);
                            rRow.RelativeItem().Element(e => StatBox(e, "Total Portfolio Value", $"BDT {r.TotalPortfolioValue:N2}"));
                            rRow.RelativeItem().Element(e => StatBox(e, "Sectors Count", r.Sectors.Count.ToString()));
                            rRow.RelativeItem().Element(e => StatBox(e, "Companies Count", r.Companies.Count.ToString()));
                        });

                        col.Item().PaddingBottom(6).Text("Sector Allocations").Bold().FontSize(11).FontColor(Colors.Blue.Darken3);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1.5f);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderStyle).Text("Sector");
                                h.Cell().Element(HeaderRightStyle).Text("Holdings");
                                h.Cell().Element(HeaderRightStyle).Text("Total Invested");
                                h.Cell().Element(HeaderRightStyle).Text("Market Value");
                                h.Cell().Element(HeaderRightStyle).Text("Unrealized P/L");
                                h.Cell().Element(HeaderRightStyle).Text("Allocation %");
                            });

                            foreach (var s in r.Sectors)
                            {
                                table.Cell().Padding(4).Text(s.SectorName).Bold();
                                table.Cell().Padding(4).AlignRight().Text(s.HoldingsCount.ToString());
                                table.Cell().Padding(4).AlignRight().Text($"BDT {s.TotalInvested:N2}");
                                table.Cell().Padding(4).AlignRight().Text($"BDT {s.CurrentMarketValue:N2}");
                                table.Cell().Padding(4).AlignRight().Text($"BDT {s.UnrealizedProfitLoss:N2}");
                                table.Cell().Padding(4).AlignRight().Text($"{s.AllocationPercentage:N2}%");
                            }
                        });

                        col.Item().PaddingTop(16).PaddingBottom(6).Text("Company Investments").Bold().FontSize(11).FontColor(Colors.Blue.Darken3);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(65);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1.5f);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderStyle).Text("Ticker");
                                h.Cell().Element(HeaderStyle).Text("Company");
                                h.Cell().Element(HeaderStyle).Text("Sector");
                                h.Cell().Element(HeaderRightStyle).Text("Shares Held");
                                h.Cell().Element(HeaderRightStyle).Text("Invested Value");
                                h.Cell().Element(HeaderRightStyle).Text("Market Value");
                                h.Cell().Element(HeaderRightStyle).Text("Allocation %");
                            });

                            foreach (var comp in r.Companies)
                            {
                                table.Cell().Padding(4).Text(comp.TickerSymbol).Bold();
                                table.Cell().Padding(4).Text(comp.CompanyName);
                                table.Cell().Padding(4).Text(comp.SectorName);
                                table.Cell().Padding(4).AlignRight().Text($"{comp.SharesHeld:N2}");
                                table.Cell().Padding(4).AlignRight().Text($"BDT {comp.TotalInvested:N2}");
                                table.Cell().Padding(4).AlignRight().Text($"BDT {comp.CurrentMarketValue:N2}");
                                table.Cell().Padding(4).AlignRight().Text($"{comp.AllocationPercentage:N2}%");
                            }
                        });
                    });
                });

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    #endregion

    #region 5. Dividend Income Export

    private async Task<ReportExportResultDto> ExportDividendsAsync(
        string format, int userId, string userName, string? portfolioName,
        ReportExportFilterDto filter, string timestamp, CancellationToken ct)
    {
        var res = await _reportService.GetDividendIncomeReportAsync(
            userId, filter.CompanyId, filter.Year, filter.StartDate, filter.EndDate, ct);
        var report = res.Data ?? new DividendIncomeReportDto();
        var fileBase = $"sharesync_dividends_{timestamp}";

        return format switch
        {
            "csv" => CreateCsvResult(BuildDividendsCsv(report, userName, filter), $"{fileBase}.csv"),
            "excel" => CreateExcelResult(BuildDividendsExcel(report, userName, filter), $"{fileBase}.xlsx"),
            "pdf" => CreatePdfResult(BuildDividendsPdf(report, userName, filter), $"{fileBase}.pdf"),
            _ => throw new AppException("Invalid format", 400)
        };
    }

    private static string BuildDividendsCsv(DividendIncomeReportDto r, string user, ReportExportFilterDto f)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ShareSync - Dividend Income Report");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC,User: {EscapeCsv(user)},Year: {f.Year?.ToString() ?? "All"}");
        sb.AppendLine($"Total Income: {r.TotalIncome:F2},This Year: {r.ThisYearIncome:F2},Upcoming: {r.UpcomingIncome:F2},Total Payments: {r.TotalPaymentsCount}");
        sb.AppendLine();
        sb.AppendLine("Declaration Date,Payment Date,Ticker,Company Name,Dividend Per Share,Shares Held,Estimated Income,Status");

        foreach (var d in r.Dividends)
        {
            sb.AppendLine(string.Join(",",
                d.DeclarationDate.ToString("yyyy-MM-dd"),
                d.PaymentDate.ToString("yyyy-MM-dd"),
                EscapeCsv(d.TickerSymbol),
                EscapeCsv(d.CompanyName),
                d.DividendPerShare.ToString("F2", CultureInfo.InvariantCulture),
                d.UserSharesHeld.ToString("F2", CultureInfo.InvariantCulture),
                d.EstimatedIncome.ToString("F2", CultureInfo.InvariantCulture),
                EscapeCsv(d.Status)
            ));
        }

        sb.AppendLine(string.Join(",",
            "TOTAL",
            "-",
            "-",
            $"{r.TotalPaymentsCount} Payments",
            "-",
            "-",
            r.TotalIncome.ToString("F2", CultureInfo.InvariantCulture),
            "-"
        ));

        return sb.ToString();
    }

    private static byte[] BuildDividendsExcel(DividendIncomeReportDto r, string user, ReportExportFilterDto f)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Dividend Income");

        ws.Cell(1, 1).Value = "ShareSync - Dividend Income Report";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC | Account: {user} | Year: {f.Year?.ToString() ?? "All"}";
        ws.Cell(2, 1).Style.Font.Italic = true;

        ws.Cell(4, 1).Value = "Total Income";
        ws.Cell(4, 2).Value = (double)r.TotalIncome;
        ws.Cell(4, 2).Style.NumberFormat.Format = "৳#,##0.00";

        ws.Cell(4, 3).Value = "This Year Income";
        ws.Cell(4, 4).Value = (double)r.ThisYearIncome;
        ws.Cell(4, 4).Style.NumberFormat.Format = "৳#,##0.00";

        ws.Cell(4, 5).Value = "Upcoming Income";
        ws.Cell(4, 6).Value = (double)r.UpcomingIncome;
        ws.Cell(4, 6).Style.NumberFormat.Format = "৳#,##0.00";

        ws.Cell(4, 7).Value = "Payments Count";
        ws.Cell(4, 8).Value = r.TotalPaymentsCount;
        ws.Range(4, 1, 4, 8).Style.Font.Bold = true;
        ws.Range(4, 1, 4, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");

        int row = 6;
        string[] headers = { "Declaration Date", "Payment Date", "Ticker", "Company Name", "Dividend/Share", "Shares Held", "Estimated Income", "Status" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F172A");
            cell.Style.Alignment.Horizontal = (i == 4 || i == 5 || i == 6) ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
        }

        row++;
        foreach (var d in r.Dividends)
        {
            ws.Cell(row, 1).Value = d.DeclarationDate.ToString("yyyy-MM-dd");
            ws.Cell(row, 2).Value = d.PaymentDate.ToString("yyyy-MM-dd");
            ws.Cell(row, 3).Value = d.TickerSymbol;
            ws.Cell(row, 4).Value = d.CompanyName;

            ws.Cell(row, 5).Value = (double)d.DividendPerShare;
            ws.Cell(row, 5).Style.NumberFormat.Format = "৳#,##0.00";

            ws.Cell(row, 6).Value = (double)d.UserSharesHeld;
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";

            ws.Cell(row, 7).Value = (double)d.EstimatedIncome;
            ws.Cell(row, 7).Style.NumberFormat.Format = "৳#,##0.00";

            ws.Cell(row, 8).Value = d.Status;
            row++;
        }

        ws.Cell(row, 1).Value = "TOTAL";
        ws.Cell(row, 4).Value = $"{r.TotalPaymentsCount} Payments";
        ws.Cell(row, 7).Value = (double)r.TotalIncome;
        ws.Cell(row, 7).Style.NumberFormat.Format = "৳#,##0.00";
        ws.Range(row, 1, row, 8).Style.Font.Bold = true;
        ws.Range(row, 1, row, 8).Style.Border.TopBorder = XLBorderStyleValues.Thin;
        ws.Range(row, 1, row, 8).Style.Border.BottomBorder = XLBorderStyleValues.Double;

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] BuildDividendsPdf(DividendIncomeReportDto r, string user, ReportExportFilterDto f)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Lato"));

                page.Header().Element(c => ComposeHeader(c, "DIVIDEND INCOME REPORT", user, null, $"Year: {f.Year?.ToString() ?? "All"}"));

                page.Content().Element(c =>
                {
                    c.Column(col =>
                    {
                        col.Item().PaddingBottom(12).Row(rRow =>
                        {
                            rRow.Spacing(10);
                            rRow.RelativeItem().Element(e => StatBox(e, "Total Income", $"BDT {r.TotalIncome:N2}"));
                            rRow.RelativeItem().Element(e => StatBox(e, "This Year Income", $"BDT {r.ThisYearIncome:N2}"));
                            rRow.RelativeItem().Element(e => StatBox(e, "Upcoming Income", $"BDT {r.UpcomingIncome:N2}"));
                            rRow.RelativeItem().Element(e => StatBox(e, "Payments Count", r.TotalPaymentsCount.ToString()));
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(70);  // Declaration
                                columns.ConstantColumn(70);  // Payment
                                columns.ConstantColumn(65);  // Ticker
                                columns.RelativeColumn(3);   // Company
                                columns.RelativeColumn(1.5f);// Div/Share
                                columns.RelativeColumn(1.5f);// Shares
                                columns.RelativeColumn(2);   // Income
                                columns.ConstantColumn(65);  // Status
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderStyle).Text("Declared");
                                h.Cell().Element(HeaderStyle).Text("Pay Date");
                                h.Cell().Element(HeaderStyle).Text("Ticker");
                                h.Cell().Element(HeaderStyle).Text("Company");
                                h.Cell().Element(HeaderRightStyle).Text("Dividend/Sh");
                                h.Cell().Element(HeaderRightStyle).Text("Shares Held");
                                h.Cell().Element(HeaderRightStyle).Text("Income (BDT)");
                                h.Cell().Element(HeaderStyle).Text("Status");
                            });

                            if (r.Dividends.Count == 0)
                            {
                                table.Cell().ColumnSpan(8).Padding(16).AlignCenter().Text("No dividend records found matching criteria.").Italic().FontColor(Colors.Grey.Medium);
                            }
                            else
                            {
                                bool alt = false;
                                foreach (var d in r.Dividends)
                                {
                                    var bg = alt ? Colors.Grey.Lighten4 : Colors.White;
                                    table.Cell().Background(bg).Padding(5).Text(d.DeclarationDate.ToString("yyyy-MM-dd"));
                                    table.Cell().Background(bg).Padding(5).Text(d.PaymentDate.ToString("yyyy-MM-dd"));
                                    table.Cell().Background(bg).Padding(5).Text(d.TickerSymbol).Bold();
                                    table.Cell().Background(bg).Padding(5).Text(d.CompanyName);
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"BDT {d.DividendPerShare:N2}");
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"{d.UserSharesHeld:N2}");
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"BDT {d.EstimatedIncome:N2}").Bold();
                                    table.Cell().Background(bg).Padding(5).Text(d.Status);
                                    alt = !alt;
                                }

                                table.Cell().ColumnSpan(4).Element(TotalStyle).Text($"TOTAL ({r.TotalPaymentsCount} Payments)");
                                table.Cell().ColumnSpan(2).Element(TotalStyle).Text("");
                                table.Cell().Element(TotalRightStyle).Text($"BDT {r.TotalIncome:N2}");
                                table.Cell().Element(TotalStyle).Text("");
                            }
                        });
                    });
                });

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    #endregion

    #region 6. Watchlist / Target Price Export

    private async Task<ReportExportResultDto> ExportWatchlistAsync(
        string format, int userId, string userName, ReportExportFilterDto filter, string timestamp, CancellationToken ct)
    {
        var res = await _reportService.GetWatchlistTargetReportAsync(userId, filter.WatchlistId, ct);
        var report = res.Data ?? new WatchlistTargetReportDto();
        var fileBase = $"sharesync_watchlist_{timestamp}";

        return format switch
        {
            "csv" => CreateCsvResult(BuildWatchlistCsv(report, userName), $"{fileBase}.csv"),
            "excel" => CreateExcelResult(BuildWatchlistExcel(report, userName), $"{fileBase}.xlsx"),
            "pdf" => CreatePdfResult(BuildWatchlistPdf(report, userName), $"{fileBase}.pdf"),
            _ => throw new AppException("Invalid format", 400)
        };
    }

    private static string BuildWatchlistCsv(WatchlistTargetReportDto r, string user)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ShareSync - Watchlist & Target Prices Report");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC,User: {EscapeCsv(user)}");
        sb.AppendLine($"Total Items: {r.TotalItems},Above Target: {r.AboveTargetCount},Near Target: {r.NearTargetCount},Below Target: {r.BelowTargetCount}");
        sb.AppendLine();
        sb.AppendLine("Watchlist,Ticker,Company Name,Current Price,Target Price,Difference,Difference %,Status");

        foreach (var w in r.Items)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(w.WatchlistName),
                EscapeCsv(w.TickerSymbol),
                EscapeCsv(w.CompanyName),
                w.CurrentPrice.ToString("F2", CultureInfo.InvariantCulture),
                w.TargetPrice.ToString("F2", CultureInfo.InvariantCulture),
                w.PriceDifference.ToString("F2", CultureInfo.InvariantCulture),
                w.PercentageDifference.ToString("F2", CultureInfo.InvariantCulture) + "%",
                EscapeCsv(w.Status)
            ));
        }

        return sb.ToString();
    }

    private static byte[] BuildWatchlistExcel(WatchlistTargetReportDto r, string user)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Watchlist Targets");

        ws.Cell(1, 1).Value = "ShareSync - Watchlist & Target Prices Report";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC | Account: {user}";
        ws.Cell(2, 1).Style.Font.Italic = true;

        ws.Cell(4, 1).Value = "Total Watched";
        ws.Cell(4, 2).Value = r.TotalItems;

        ws.Cell(4, 3).Value = "Above Target";
        ws.Cell(4, 4).Value = r.AboveTargetCount;

        ws.Cell(4, 5).Value = "Near Target";
        ws.Cell(4, 6).Value = r.NearTargetCount;

        ws.Cell(4, 7).Value = "Below Target";
        ws.Cell(4, 8).Value = r.BelowTargetCount;
        ws.Range(4, 1, 4, 8).Style.Font.Bold = true;
        ws.Range(4, 1, 4, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");

        int row = 6;
        string[] headers = { "Watchlist", "Ticker", "Company Name", "Current Price", "Target Price", "Difference", "Diff %", "Status" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F172A");
            cell.Style.Alignment.Horizontal = (i >= 3 && i <= 6) ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
        }

        row++;
        foreach (var item in r.Items)
        {
            ws.Cell(row, 1).Value = item.WatchlistName;
            ws.Cell(row, 2).Value = item.TickerSymbol;
            ws.Cell(row, 3).Value = item.CompanyName;

            ws.Cell(row, 4).Value = (double)item.CurrentPrice;
            ws.Cell(row, 4).Style.NumberFormat.Format = "৳#,##0.00";

            ws.Cell(row, 5).Value = (double)item.TargetPrice;
            ws.Cell(row, 5).Style.NumberFormat.Format = "৳#,##0.00";

            ws.Cell(row, 6).Value = (double)item.PriceDifference;
            ws.Cell(row, 6).Style.NumberFormat.Format = "৳#,##0.00";

            ws.Cell(row, 7).Value = (double)(item.PercentageDifference / 100m);
            ws.Cell(row, 7).Style.NumberFormat.Format = "0.00%";

            ws.Cell(row, 8).Value = item.Status;
            row++;
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] BuildWatchlistPdf(WatchlistTargetReportDto r, string user)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Lato"));

                page.Header().Element(c => ComposeHeader(c, "WATCHLIST & TARGET PRICE REPORT", user, null));

                page.Content().Element(c =>
                {
                    c.Column(col =>
                    {
                        col.Item().PaddingBottom(12).Row(rRow =>
                        {
                            rRow.Spacing(10);
                            rRow.RelativeItem().Element(e => StatBox(e, "Total Items", r.TotalItems.ToString()));
                            rRow.RelativeItem().Element(e => StatBox(e, "Above Target", r.AboveTargetCount.ToString()));
                            rRow.RelativeItem().Element(e => StatBox(e, "Near Target", r.NearTargetCount.ToString()));
                            rRow.RelativeItem().Element(e => StatBox(e, "Below Target", r.BelowTargetCount.ToString()));
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);   // Watchlist
                                columns.ConstantColumn(65);  // Ticker
                                columns.RelativeColumn(3);   // Company
                                columns.RelativeColumn(1.5f);// Current
                                columns.RelativeColumn(1.5f);// Target
                                columns.RelativeColumn(1.5f);// Diff
                                columns.RelativeColumn(1.5f);// Diff %
                                columns.RelativeColumn(2);   // Status
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderStyle).Text("Watchlist");
                                h.Cell().Element(HeaderStyle).Text("Ticker");
                                h.Cell().Element(HeaderStyle).Text("Company");
                                h.Cell().Element(HeaderRightStyle).Text("Current Price");
                                h.Cell().Element(HeaderRightStyle).Text("Target Price");
                                h.Cell().Element(HeaderRightStyle).Text("Difference");
                                h.Cell().Element(HeaderRightStyle).Text("Diff %");
                                h.Cell().Element(HeaderStyle).Text("Status");
                            });

                            if (r.Items.Count == 0)
                            {
                                table.Cell().ColumnSpan(8).Padding(16).AlignCenter().Text("No watchlist target items found.").Italic().FontColor(Colors.Grey.Medium);
                            }
                            else
                            {
                                bool alt = false;
                                foreach (var item in r.Items)
                                {
                                    var bg = alt ? Colors.Grey.Lighten4 : Colors.White;
                                    table.Cell().Background(bg).Padding(5).Text(item.WatchlistName);
                                    table.Cell().Background(bg).Padding(5).Text(item.TickerSymbol).Bold();
                                    table.Cell().Background(bg).Padding(5).Text(item.CompanyName);
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"BDT {item.CurrentPrice:N2}");
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"BDT {item.TargetPrice:N2}");
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"BDT {item.PriceDifference:N2}");
                                    table.Cell().Background(bg).Padding(5).AlignRight().Text($"{item.PercentageDifference:N2}%");
                                    table.Cell().Background(bg).Padding(5).Text(item.Status);
                                    alt = !alt;
                                }
                            }
                        });
                    });
                });

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    #endregion

    #region Shared Helpers & Styles

    private static ReportExportResultDto CreateCsvResult(string csv, string filename)
    {
        return new ReportExportResultDto
        {
            FileContents = Encoding.UTF8.GetBytes(csv),
            ContentType = "text/csv; charset=utf-8",
            FileName = filename
        };
    }

    private static ReportExportResultDto CreateExcelResult(byte[] bytes, string filename)
    {
        return new ReportExportResultDto
        {
            FileContents = bytes,
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName = filename
        };
    }

    private static ReportExportResultDto CreatePdfResult(byte[] bytes, string filename)
    {
        return new ReportExportResultDto
        {
            FileContents = bytes,
            ContentType = "application/pdf",
            FileName = filename
        };
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";

        var sanitized = value;
        // Formula injection protection
        var trimmed = value.TrimStart();
        if (trimmed.StartsWith('=') || trimmed.StartsWith('+') || trimmed.StartsWith('@') || trimmed.StartsWith('\t') || trimmed.StartsWith('\r'))
        {
            sanitized = "'" + value;
        }

        if (sanitized.Contains(',') || sanitized.Contains('"') || sanitized.Contains('\n') || sanitized.Contains('\r'))
        {
            return "\"" + sanitized.Replace("\"", "\"\"") + "\"";
        }

        return sanitized;
    }

    private static void ComposeHeader(IContainer container, string reportTitle, string user, string? portfolio, string? extraFilter = null)
    {
        container.PaddingBottom(12).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Row(brandRow =>
                {
                    brandRow.Spacing(4);
                    brandRow.AutoItem().Text("Share").Bold().FontSize(16).FontColor(Color.FromHex("#0F172A"));
                    brandRow.AutoItem().Text("Sync").Bold().FontSize(16).FontColor(Color.FromHex("#1D4ED8"));
                });
                col.Item().PaddingTop(2).Text(reportTitle).Bold().FontSize(13).FontColor(Color.FromHex("#0F172A"));
            });

            row.RelativeItem().AlignRight().Column(col =>
            {
                col.Item().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(8).FontColor(Colors.Grey.Medium);
                col.Item().Text($"Account: {user} | Portfolio: {portfolio ?? "All Portfolios"}").FontSize(8).FontColor(Colors.Grey.Darken1);
                if (!string.IsNullOrWhiteSpace(extraFilter))
                {
                    col.Item().Text(extraFilter).FontSize(8).FontColor(Colors.Grey.Darken1);
                }
            });
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.PaddingTop(10).BorderTop(1).BorderColor(Colors.Grey.Lighten2).Row(row =>
        {
            row.RelativeItem().Text("ShareSync Investment Platform • Authoritative Financial Report").FontSize(8).FontColor(Colors.Grey.Medium);
            row.RelativeItem().AlignRight().Text(x =>
            {
                x.Span("Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                x.Span(" of ").FontSize(8).FontColor(Colors.Grey.Medium);
                x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
            });
        });
    }

    private static void StatBox(IContainer container, string label, string value, bool? positive = null)
    {
        var textColor = positive.HasValue
            ? (positive.Value ? Colors.Green.Darken2 : Colors.Red.Darken2)
            : Color.FromHex("#0F172A");

        container
            .Background(Colors.Grey.Lighten4)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(6)
            .Column(c =>
            {
                c.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
                c.Item().Text(value).Bold().FontSize(11).FontColor(textColor);
            });
    }

    private static IContainer HeaderStyle(IContainer container) =>
        container.Background(Color.FromHex("#0F172A")).Padding(5).DefaultTextStyle(x => x.Bold().FontColor(Colors.White));

    private static IContainer HeaderRightStyle(IContainer container) =>
        container.Background(Color.FromHex("#0F172A")).Padding(5).AlignRight().DefaultTextStyle(x => x.Bold().FontColor(Colors.White));

    private static IContainer TotalStyle(IContainer container) =>
        container.Background(Colors.Grey.Lighten3).Padding(5).BorderTop(1).BorderColor(Colors.Grey.Darken1).DefaultTextStyle(x => x.Bold());

    private static IContainer TotalRightStyle(IContainer container) =>
        container.Background(Colors.Grey.Lighten3).Padding(5).AlignRight().BorderTop(1).BorderColor(Colors.Grey.Darken1).DefaultTextStyle(x => x.Bold());

    #endregion
}
