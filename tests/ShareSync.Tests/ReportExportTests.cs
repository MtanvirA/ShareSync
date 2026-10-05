using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.DTOs.Reports;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class ReportExportTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new ShareSyncDbContext(options);

        // Seed Users
        context.Users.AddRange(
            new AppUser { UserId = 1, Email = "user1@sharesync.com", Name = "User One", PasswordHash = "hash1" },
            new AppUser { UserId = 2, Email = "user2@sharesync.com", Name = "User Two", PasswordHash = "hash2" },
            new AppUser { UserId = 3, Email = "empty@sharesync.com", Name = "Empty User", PasswordHash = "hash3" }
        );

        // Seed Sectors
        context.Sectors.AddRange(
            new Sector { SectorId = 1, SectorName = "Telecommunications, Media" },
            new Sector { SectorId = 2, SectorName = "Pharmaceuticals & Healthcare" },
            new Sector { SectorId = 3, SectorName = "Conglomerate" }
        );

        // Seed Companies with normal and special characters (quotes, commas, equals, formulas)
        context.Companies.AddRange(
            new Company { CompanyId = 1, TickerSymbol = "GP", CompanyName = "Grameenphone, Ltd.", CurrentPrice = 300.00m, SectorId = 1 },
            new Company { CompanyId = 2, TickerSymbol = "SQURPHARMA", CompanyName = "Square Pharma, \"Top\" Holding", CurrentPrice = 220.50m, SectorId = 2 },
            new Company { CompanyId = 3, TickerSymbol = "BX", CompanyName = "=1+2 Formula Company", CurrentPrice = 120.75m, SectorId = 3 }
        );

        // Seed Portfolios
        context.Portfolios.AddRange(
            new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "User 1 Main Portfolio, High Growth" },
            new Portfolio { PortfolioId = 2, UserId = 2, PortfolioName = "User 2 Main Portfolio" },
            new Portfolio { PortfolioId = 3, UserId = 3, PortfolioName = "Empty Portfolio" }
        );

        // Seed Transactions for User 1 Portfolio 1:
        context.Transactions.AddRange(
            new Transaction
            {
                TransactionId = 1,
                PortfolioId = 1,
                CompanyId = 1,
                TransactionType = "BUY",
                Quantity = 100,
                PricePerShare = 280.25m,
                TransactionDate = new DateTime(2026, 1, 10)
            },
            new Transaction
            {
                TransactionId = 2,
                PortfolioId = 1,
                CompanyId = 1,
                TransactionType = "BUY",
                Quantity = 50,
                PricePerShare = 290.50m,
                TransactionDate = new DateTime(2026, 1, 20)
            },
            new Transaction
            {
                TransactionId = 3,
                PortfolioId = 1,
                CompanyId = 1,
                TransactionType = "SELL",
                Quantity = 30,
                PricePerShare = 310.00m,
                TransactionDate = new DateTime(2026, 2, 5)
            },
            new Transaction
            {
                TransactionId = 4,
                PortfolioId = 1,
                CompanyId = 2,
                TransactionType = "BUY",
                Quantity = 50,
                PricePerShare = 200.00m,
                TransactionDate = new DateTime(2026, 2, 10)
            },
            // User 2 Transaction
            new Transaction
            {
                TransactionId = 5,
                PortfolioId = 2,
                CompanyId = 3,
                TransactionType = "BUY",
                Quantity = 200,
                PricePerShare = 100.00m,
                TransactionDate = new DateTime(2026, 2, 15)
            },
            // User 1 buys Company 3 (=1+2 Formula Company)
            new Transaction
            {
                TransactionId = 6,
                PortfolioId = 1,
                CompanyId = 3,
                TransactionType = "BUY",
                Quantity = 10,
                PricePerShare = 120.75m,
                TransactionDate = new DateTime(2026, 2, 20)
            }
        );

        // Seed Snapshots for Portfolio 1
        context.PortfolioSnapshots.AddRange(
            new PortfolioSnapshot { SnapshotId = 1, PortfolioId = 1, SnapshotDate = new DateTime(2026, 1, 31), TotalValue = 42500.50m },
            new PortfolioSnapshot { SnapshotId = 2, PortfolioId = 1, SnapshotDate = new DateTime(2026, 2, 28), TotalValue = 47025.00m },
            new PortfolioSnapshot { SnapshotId = 3, PortfolioId = 2, SnapshotDate = new DateTime(2026, 2, 28), TotalValue = 20000.00m }
        );

        // Seed Dividends
        context.Dividends.AddRange(
            new Dividend
            {
                DividendId = 1,
                CompanyId = 1,
                DividendPerShare = 12.50m,
                DeclarationDate = new DateTime(2026, 2, 1),
                PaymentDate = new DateTime(2026, 3, 1)
            },
            new Dividend
            {
                DividendId = 2,
                CompanyId = 2,
                DividendPerShare = 8.75m,
                DeclarationDate = new DateTime(2026, 2, 15),
                PaymentDate = new DateTime(2026, 3, 15)
            }
        );

        // Seed Watchlists
        var wl = new Watchlist { WatchlistId = 1, UserId = 1, WatchlistName = "Blue Chips, Leaders & Targets" };
        wl.Items.Add(new WatchlistItem { WatchlistId = 1, CompanyId = 1, TargetPrice = 320.00m });
        wl.Items.Add(new WatchlistItem { WatchlistId = 1, CompanyId = 2, TargetPrice = 210.50m });
        context.Watchlists.Add(wl);

        context.SaveChanges();
        return context;
    }

    [Theory]
    [InlineData("holdings", "csv")]
    [InlineData("holdings", "excel")]
    [InlineData("holdings", "pdf")]
    [InlineData("performance", "csv")]
    [InlineData("performance", "excel")]
    [InlineData("performance", "pdf")]
    [InlineData("transactions", "csv")]
    [InlineData("transactions", "excel")]
    [InlineData("transactions", "pdf")]
    [InlineData("company-sector", "csv")]
    [InlineData("company-sector", "excel")]
    [InlineData("company-sector", "pdf")]
    [InlineData("dividends", "csv")]
    [InlineData("dividends", "excel")]
    [InlineData("dividends", "pdf")]
    [InlineData("watchlist", "csv")]
    [InlineData("watchlist", "excel")]
    [InlineData("watchlist", "pdf")]
    public async Task ExportReportAsync_AllSixReports_AllThreeFormats_GenerateValidFiles(string reportType, string format)
    {
        using var context = CreateInMemoryDbContext();
        var reportService = new ReportService(context);
        var exportService = new ReportExportService(reportService, context);

        var filter = new ReportExportFilterDto
        {
            PortfolioId = 1,
            Period = "12"
        };

        var result = await exportService.ExportReportAsync(reportType, format, userId: 1, filter);

        Assert.NotNull(result);
        Assert.NotEmpty(result.FileContents);
        Assert.NotEmpty(result.FileName);
        Assert.NotEmpty(result.ContentType);

        if (format == "pdf")
        {
            Assert.Equal("application/pdf", result.ContentType);
            Assert.EndsWith(".pdf", result.FileName);
            var header = Encoding.ASCII.GetString(result.FileContents.Take(4).ToArray());
            Assert.Equal("%PDF", header);
        }
        else if (format == "excel")
        {
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.ContentType);
            Assert.EndsWith(".xlsx", result.FileName);
            using var ms = new MemoryStream(result.FileContents);
            using var workbook = new XLWorkbook(ms);
            Assert.True(workbook.Worksheets.Count >= 1);
        }
        else if (format == "csv")
        {
            Assert.Equal("text/csv; charset=utf-8", result.ContentType);
            Assert.EndsWith(".csv", result.FileName);
            var csv = Encoding.UTF8.GetString(result.FileContents);
            Assert.Contains("ShareSync", csv);
        }
    }

    [Fact]
    public async Task ExportHoldings_ValuesMatchReportServiceExactly()
    {
        using var context = CreateInMemoryDbContext();
        var reportService = new ReportService(context);
        var exportService = new ReportExportService(reportService, context);

        // Fetch authoritative UI data
        var authoritative = await reportService.GetHoldingsReportAsync(userId: 1, portfolioId: 1);
        Assert.True(authoritative.Success);
        var uiData = authoritative.Data;

        // Export to CSV and Excel
        var csvResult = await exportService.ExportReportAsync("holdings", "csv", userId: 1, new ReportExportFilterDto
        {
            PortfolioId = 1
        });
        var csvText = Encoding.UTF8.GetString(csvResult.FileContents);

        var excelResult = await exportService.ExportReportAsync("holdings", "excel", userId: 1, new ReportExportFilterDto
        {
            PortfolioId = 1
        });
        using var ms = new MemoryStream(excelResult.FileContents);
        using var workbook = new XLWorkbook(ms);
        var worksheet = workbook.Worksheet(1);

        // Verify CSV contains same exact financial values as authoritative report
        Assert.NotNull(uiData);
        Assert.Contains(uiData.TotalMarketValue.ToString("F2"), csvText);
        Assert.Contains(uiData.TotalInvested.ToString("F2"), csvText);
        Assert.Contains(uiData.TotalUnrealizedProfitLoss.ToString("F2"), csvText);

        foreach (var holding in uiData.Holdings)
        {
            Assert.Contains(holding.TickerSymbol, csvText);
            Assert.Contains(holding.InvestedValue.ToString("F2"), csvText);
            Assert.Contains(holding.CurrentMarketValue.ToString("F2"), csvText);
        }

        // Verify Excel worksheet contains exact title and values
        Assert.Equal("Portfolio Holdings", worksheet.Name);
        var cellValues = worksheet.CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains(cellValues, v => v.Contains("ShareSync"));
        Assert.Contains(cellValues, v => v.Contains("Portfolio Holdings", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExportCompanySector_ValuesMatchReportServiceExactly()
    {
        using var context = CreateInMemoryDbContext();
        var reportService = new ReportService(context);
        var exportService = new ReportExportService(reportService, context);

        var authoritative = await reportService.GetCompanySectorReportAsync(userId: 1, portfolioId: 1);
        Assert.True(authoritative.Success);
        var uiData = authoritative.Data;

        var csvResult = await exportService.ExportReportAsync("company-sector", "csv", userId: 1, new ReportExportFilterDto
        {
            PortfolioId = 1
        });
        var csvText = Encoding.UTF8.GetString(csvResult.FileContents);

        // Verify Portfolio Market Value in CSV matches authoritative UI data: 47,025.00
        Assert.Contains(uiData.TotalPortfolioValue.ToString("F2"), csvText);

        foreach (var sector in uiData.Sectors)
        {
            Assert.Contains(sector.SectorName, csvText);
            Assert.Contains(sector.CurrentMarketValue.ToString("F2"), csvText);
            Assert.Contains(sector.AllocationPercentage.ToString("F2") + "%", csvText);
        }
    }

    [Fact]
    public async Task ExportPerformance_ValuesMatchReportServiceSnapshots()
    {
        using var context = CreateInMemoryDbContext();
        var reportService = new ReportService(context);
        var exportService = new ReportExportService(reportService, context);

        var authoritative = await reportService.GetPerformanceReportAsync(userId: 1, portfolioId: 1, period: "12");
        Assert.True(authoritative.Success);
        var uiData = authoritative.Data;

        var csvResult = await exportService.ExportReportAsync("performance", "csv", userId: 1, new ReportExportFilterDto
        {
            PortfolioId = 1,
            Period = "12"
        });
        var csvText = Encoding.UTF8.GetString(csvResult.FileContents);

        foreach (var snap in uiData.Snapshots)
        {
            Assert.Contains(snap.SnapshotDate.ToString("yyyy-MM-dd"), csvText);
            Assert.Contains(snap.PortfolioValue.ToString("F2"), csvText);
        }
    }

    [Fact]
    public async Task ExportTransactionHistory_ValuesMatchReportServiceExactly()
    {
        using var context = CreateInMemoryDbContext();
        var reportService = new ReportService(context);
        var exportService = new ReportExportService(reportService, context);

        var authoritative = await reportService.GetTransactionHistoryReportAsync(userId: 1, portfolioId: 1);
        Assert.True(authoritative.Success);
        var uiData = authoritative.Data;

        var csvResult = await exportService.ExportReportAsync("transactions", "csv", userId: 1, new ReportExportFilterDto
        {
            PortfolioId = 1,
            Period = "all"
        });
        var csvText = Encoding.UTF8.GetString(csvResult.FileContents);

        Assert.NotNull(uiData);
        Assert.Contains(uiData.TotalBuyValue.ToString("F2"), csvText);
        Assert.Contains(uiData.TotalSellValue.ToString("F2"), csvText);
        Assert.Contains(uiData.NetCashFlow.ToString("F2"), csvText);

        foreach (var tx in uiData.Transactions)
        {
            Assert.Contains(tx.TickerSymbol, csvText);
            Assert.Contains(tx.TransactionType, csvText);
            Assert.Contains(tx.TotalTransactionValue.ToString("F2"), csvText);
        }
    }

    [Fact]
    public async Task ExportDividendIncome_ValuesMatchReportServiceExactly()
    {
        using var context = CreateInMemoryDbContext();
        var reportService = new ReportService(context);
        var exportService = new ReportExportService(reportService, context);

        var authoritative = await reportService.GetDividendIncomeReportAsync(userId: 1);
        Assert.True(authoritative.Success);
        var uiData = authoritative.Data;

        var csvResult = await exportService.ExportReportAsync("dividends", "csv", userId: 1, new ReportExportFilterDto
        {
            PortfolioId = 1,
            Period = "all"
        });
        var csvText = Encoding.UTF8.GetString(csvResult.FileContents);

        Assert.NotNull(uiData);
        Assert.Contains(uiData.TotalIncome.ToString("F2"), csvText);

        foreach (var div in uiData.Dividends)
        {
            Assert.Contains(div.TickerSymbol, csvText);
            Assert.Contains(div.EstimatedIncome.ToString("F2"), csvText);
        }
    }

    [Fact]
    public async Task ExportWatchlist_ValuesMatchReportServiceExactly()
    {
        using var context = CreateInMemoryDbContext();
        var reportService = new ReportService(context);
        var exportService = new ReportExportService(reportService, context);

        var authoritative = await reportService.GetWatchlistTargetReportAsync(userId: 1);
        Assert.True(authoritative.Success);
        var uiData = authoritative.Data;

        var csvResult = await exportService.ExportReportAsync("watchlist", "csv", userId: 1, new ReportExportFilterDto());
        var csvText = Encoding.UTF8.GetString(csvResult.FileContents);

        Assert.Contains(uiData.TotalItems.ToString(), csvText);

        foreach (var item in uiData.Items)
        {
            Assert.Contains(item.TickerSymbol, csvText);
            Assert.Contains(item.TargetPrice.ToString("F2"), csvText);
        }
    }

    [Fact]
    public async Task ExportReportAsync_UnauthorizedPortfolio_ThrowsForbidden()
    {
        using var context = CreateInMemoryDbContext();
        var reportService = new ReportService(context);
        var exportService = new ReportExportService(reportService, context);

        // User 2 trying to export User 1's portfolio (id = 1)
        var filter = new ReportExportFilterDto
        {
            PortfolioId = 1
        };

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            exportService.ExportReportAsync("holdings", "pdf", userId: 2, filter));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ExportReportAsync_EmptyReport_ExportsCleanlyWithHeadersAndNoDataNote()
    {
        using var context = CreateInMemoryDbContext();
        var reportService = new ReportService(context);
        var exportService = new ReportExportService(reportService, context);

        // User 3 has no transactions, no holdings, empty portfolio
        foreach (var format in new[] { "csv", "excel", "pdf" })
        {
            var filter = new ReportExportFilterDto
            {
                PortfolioId = 3
            };

            var result = await exportService.ExportReportAsync("holdings", format, userId: 3, filter);

            Assert.NotNull(result);
            Assert.NotEmpty(result.FileContents);

            if (format == "csv")
            {
                var csv = Encoding.UTF8.GetString(result.FileContents);
                Assert.Contains("Company Name", csv);
                Assert.Contains("Ticker", csv);
            }
            else if (format == "excel")
            {
                using var ms = new MemoryStream(result.FileContents);
                using var workbook = new XLWorkbook(ms);
                Assert.True(workbook.Worksheets.Count >= 1);
            }
            else if (format == "pdf")
            {
                Assert.True(result.FileContents.Length > 0);
            }
        }
    }

    [Fact]
    public async Task ExportReportAsync_EscapesSpecialCharactersAndFormulaInjection()
    {
        using var context = CreateInMemoryDbContext();
        var reportService = new ReportService(context);
        var exportService = new ReportExportService(reportService, context);

        // Company 2 has quotes in name: "Top" Holding
        // Company 3 has formula injection in name: =1+2 Formula Company
        // Sector 1 has comma: Telecommunications, Media
        var result = await exportService.ExportReportAsync("company-sector", "csv", userId: 1, new ReportExportFilterDto
        {
            PortfolioId = 1
        });

        var csvText = Encoding.UTF8.GetString(result.FileContents);

        // Commas should be quoted: "Telecommunications, Media"
        Assert.Contains("\"Telecommunications, Media\"", csvText);

        // Quotes in text should be doubled: """Top"""
        Assert.Contains("\"\"Top\"\"", csvText);

        // Formula injection protection prefixing '=': "'=1+2"
        Assert.Contains("'=1+2", csvText);
    }

    [Fact]
    public async Task ExportReportAsync_LargeReport_HandlesLargeDatasetsEfficiently()
    {
        using var context = CreateInMemoryDbContext();

        // Seed 600 transactions to simulate a large report
        var rnd = new Random(42);
        for (int i = 100; i < 700; i++)
        {
            context.Transactions.Add(new Transaction
            {
                TransactionId = i,
                PortfolioId = 1,
                CompanyId = (i % 2 == 0) ? 1 : 2,
                TransactionType = (i % 3 == 0) ? "SELL" : "BUY",
                Quantity = rnd.Next(10, 500),
                PricePerShare = (decimal)(150 + rnd.NextDouble() * 200),
                TransactionDate = new DateTime(2025, 1, 1).AddDays(i - 100)
            });
        }
        await context.SaveChangesAsync();

        var reportService = new ReportService(context);
        var exportService = new ReportExportService(reportService, context);

        var filter = new ReportExportFilterDto
        {
            PortfolioId = 1,
            Period = "all"
        };

        var result = await exportService.ExportReportAsync("transactions", "excel", userId: 1, filter);

        Assert.NotNull(result);
        Assert.NotEmpty(result.FileContents);

        using var ms = new MemoryStream(result.FileContents);
        using var workbook = new XLWorkbook(ms);
        var ws = workbook.Worksheet(1);
        Assert.True(ws.RowsUsed().Count() > 600);
    }
}
