using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class CsvTransactionImportTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new ShareSyncDbContext(options);

        // Seed users
        var user1 = new AppUser { UserId = 1, Name = "User One", Email = "user1@sharesync.com", PasswordHash = "hash" };
        var user2 = new AppUser { UserId = 2, Name = "User Two", Email = "user2@sharesync.com", PasswordHash = "hash" };
        context.Users.AddRange(user1, user2);

        // Seed portfolios
        var p1 = new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "Tech Growth" };
        var p2 = new Portfolio { PortfolioId = 2, UserId = 1, PortfolioName = "Dividend Fund" };
        var pOther = new Portfolio { PortfolioId = 3, UserId = 2, PortfolioName = "User 2 Portfolio" };
        context.Portfolios.AddRange(p1, p2, pOther);

        // Seed companies
        var sector = new Sector { SectorId = 1, SectorName = "Telecommunication" };
        context.Sectors.Add(sector);

        var c1 = new Company { CompanyId = 1, CompanyName = "Grameenphone Ltd.", TickerSymbol = "GP", SectorId = 1, CurrentPrice = 300m };
        var c2 = new Company { CompanyId = 2, CompanyName = "British American Tobacco", TickerSymbol = "BATBC", SectorId = 1, CurrentPrice = 520m };
        var c3 = new Company { CompanyId = 3, CompanyName = "Beximco Pharmaceuticals", TickerSymbol = "BEXIMCO", SectorId = 1, CurrentPrice = 115m };
        context.Companies.AddRange(c1, c2, c3);

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task ValidateCsv_ValidCsv_SucceedsWithPreview()
    {
        using var context = CreateInMemoryDbContext();
        var service = new CsvTransactionImportService(context);

        var csv = @"transaction_date,company_ticker,transaction_type,quantity,price_per_share,notes
2026-10-01,GP,BUY,100,410.00,First BUY
2026-10-02,BATBC,BUY,50,518.50,Second BUY
2026-10-03,GP,SELL,20,425.00,Partial profit taking";

        var response = await service.ValidateCsvAsync(portfolioId: 1, csvContent: csv, userId: 1);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.True(response.Data.IsValid);
        Assert.Equal(3, response.Data.TotalRows);
        Assert.Equal(3, response.Data.ValidRowsCount);
        Assert.Equal(0, response.Data.InvalidRowsCount);
        Assert.Equal(100 * 410m + 50 * 518.5m + 20 * 425m, response.Data.TotalEstimatedAmount);
        Assert.Empty(response.Data.Errors);
    }

    [Fact]
    public async Task ValidateCsv_EmptyCsv_ThrowsAppException()
    {
        using var context = CreateInMemoryDbContext();
        var service = new CsvTransactionImportService(context);

        await Assert.ThrowsAsync<AppException>(() =>
            service.ValidateCsvAsync(portfolioId: 1, csvContent: "", userId: 1));

        await Assert.ThrowsAsync<AppException>(() =>
            service.ValidateCsvAsync(portfolioId: 1, csvContent: "   \r\n   ", userId: 1));
    }

    [Fact]
    public async Task ValidateCsv_MalformedCsv_ReportsRowStructureError()
    {
        using var context = CreateInMemoryDbContext();
        var service = new CsvTransactionImportService(context);

        var csv = @"transaction_date,company_ticker,transaction_type,quantity,price_per_share
2026-10-01,GP,BUY,100,410
2026-10-02,BATBC,BUY"; // Only 3 columns instead of 5

        var response = await service.ValidateCsvAsync(portfolioId: 1, csvContent: csv, userId: 1);

        Assert.NotNull(response.Data);
        Assert.False(response.Data.IsValid);
        Assert.Equal(1, response.Data.InvalidRowsCount);

        var row2 = response.Data.Rows.First(r => r.RowNumber == 2);
        Assert.False(row2.IsValid);
        Assert.Contains(row2.Errors, e => e.Field == "csv_structure");
    }

    [Fact]
    public async Task ValidateCsv_MissingColumns_ThrowsAppException()
    {
        using var context = CreateInMemoryDbContext();
        var service = new CsvTransactionImportService(context);

        var csv = @"transaction_date,company_ticker,quantity,price_per_share
2026-10-01,GP,100,410"; // Missing transaction_type

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.ValidateCsvAsync(portfolioId: 1, csvContent: csv, userId: 1));

        Assert.Contains("transaction_type", ex.Message);
    }

    [Fact]
    public async Task ValidateCsv_InvalidTicker_ReportsCompanyNotFoundError()
    {
        using var context = CreateInMemoryDbContext();
        var service = new CsvTransactionImportService(context);

        var csv = @"transaction_date,company_ticker,transaction_type,quantity,price_per_share
2026-10-01,UNKNOWN_TICKER,BUY,100,410";

        var response = await service.ValidateCsvAsync(portfolioId: 1, csvContent: csv, userId: 1);

        Assert.NotNull(response.Data);
        Assert.False(response.Data.IsValid);
        var err = Assert.Single(response.Data.Rows[0].Errors);
        Assert.Equal("company_ticker", err.Field);
        Assert.Contains("UNKNOWN_TICKER", err.Problem);
        Assert.False(string.IsNullOrWhiteSpace(err.SuggestedCorrection));
    }

    [Fact]
    public async Task ValidateCsv_InvalidType_ReportsTransactionTypeError()
    {
        using var context = CreateInMemoryDbContext();
        var service = new CsvTransactionImportService(context);

        var csv = @"transaction_date,company_ticker,transaction_type,quantity,price_per_share
2026-10-01,GP,HOLD,100,410";

        var response = await service.ValidateCsvAsync(portfolioId: 1, csvContent: csv, userId: 1);

        Assert.NotNull(response.Data);
        Assert.False(response.Data.IsValid);
        var err = Assert.Single(response.Data.Rows[0].Errors);
        Assert.Equal("transaction_type", err.Field);
        Assert.Contains("HOLD", err.Problem);
    }

    [Fact]
    public async Task ValidateCsv_NegativeQuantity_ReportsQuantityError()
    {
        using var context = CreateInMemoryDbContext();
        var service = new CsvTransactionImportService(context);

        var csv = @"transaction_date,company_ticker,transaction_type,quantity,price_per_share
2026-10-01,GP,BUY,-50,410
2026-10-01,GP,BUY,0,410";

        var response = await service.ValidateCsvAsync(portfolioId: 1, csvContent: csv, userId: 1);

        Assert.NotNull(response.Data);
        Assert.False(response.Data.IsValid);
        Assert.Contains(response.Data.Rows[0].Errors, e => e.Field == "quantity");
        Assert.Contains(response.Data.Rows[1].Errors, e => e.Field == "quantity");
    }

    [Fact]
    public async Task ValidateCsv_NegativePrice_ReportsPriceError()
    {
        using var context = CreateInMemoryDbContext();
        var service = new CsvTransactionImportService(context);

        var csv = @"transaction_date,company_ticker,transaction_type,quantity,price_per_share
2026-10-01,GP,BUY,50,-410
2026-10-01,GP,BUY,50,0";

        var response = await service.ValidateCsvAsync(portfolioId: 1, csvContent: csv, userId: 1);

        Assert.NotNull(response.Data);
        Assert.False(response.Data.IsValid);
        Assert.Contains(response.Data.Rows[0].Errors, e => e.Field == "price_per_share");
        Assert.Contains(response.Data.Rows[1].Errors, e => e.Field == "price_per_share");
    }

    [Fact]
    public async Task ValidateCsv_Oversell_ReportsOversellError()
    {
        using var context = CreateInMemoryDbContext();
        var service = new CsvTransactionImportService(context);

        // User initially has 0 shares of GP in portfolio 1.
        // Row 1: SELL 10 (fails oversell, 0 available)
        // Row 2: BUY 50
        // Row 3: SELL 60 (fails oversell, only 50 available)
        var csv = @"transaction_date,company_ticker,transaction_type,quantity,price_per_share
2026-10-01,GP,SELL,10,410
2026-10-02,GP,BUY,50,400
2026-10-03,GP,SELL,60,420";

        var response = await service.ValidateCsvAsync(portfolioId: 1, csvContent: csv, userId: 1);

        Assert.NotNull(response.Data);
        Assert.False(response.Data.IsValid);
        Assert.Equal(2, response.Data.InvalidRowsCount);

        var row1Err = Assert.Single(response.Data.Rows[0].Errors);
        Assert.Equal("sell_availability", row1Err.Field);
        Assert.Contains("Oversell error", row1Err.Problem);

        Assert.True(response.Data.Rows[1].IsValid); // Row 2 BUY 50 succeeds

        var row3Err = Assert.Single(response.Data.Rows[2].Errors);
        Assert.Equal("sell_availability", row3Err.Field);
        Assert.Contains("only 50", row3Err.Problem);
    }

    [Fact]
    public async Task ValidateCsv_DuplicateRows_ReportsDuplicateRowError()
    {
        using var context = CreateInMemoryDbContext();
        var service = new CsvTransactionImportService(context);

        var csv = @"transaction_date,company_ticker,transaction_type,quantity,price_per_share
2026-10-01,GP,BUY,100,410
2026-10-01,GP,BUY,100,410"; // Identical duplicate of Row 1

        var response = await service.ValidateCsvAsync(portfolioId: 1, csvContent: csv, userId: 1);

        Assert.NotNull(response.Data);
        Assert.False(response.Data.IsValid);
        Assert.True(response.Data.Rows[0].IsValid);
        Assert.False(response.Data.Rows[1].IsValid);

        var err = Assert.Single(response.Data.Rows[1].Errors);
        Assert.Equal("duplicate_row", err.Field);
        Assert.Contains("duplicate of Row 1", err.Problem);
    }

    [Fact]
    public async Task ValidateCsv_VeryLargeFile_ThrowsAppException()
    {
        using var context = CreateInMemoryDbContext();
        var service = new CsvTransactionImportService(context);

        // Exceed row limit (1,000 max rows)
        var sb = new StringBuilder();
        sb.AppendLine("transaction_date,company_ticker,transaction_type,quantity,price_per_share");
        for (int i = 0; i < 1005; i++)
        {
            sb.AppendLine($"2026-10-01,GP,BUY,{i + 1},410");
        }

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.ValidateCsvAsync(portfolioId: 1, csvContent: sb.ToString(), userId: 1));

        Assert.Contains("1000", ex.Message);
    }

    [Fact]
    public async Task ValidateCsv_MaliciousCsvContent_SanitizesFormulaInjection()
    {
        using var context = CreateInMemoryDbContext();
        var service = new CsvTransactionImportService(context);

        var csv = @"transaction_date,company_ticker,transaction_type,quantity,price_per_share,notes
2026-10-01,GP,BUY,100,410,""=cmd|'/C calc'!A0""";

        var response = await service.ValidateCsvAsync(portfolioId: 1, csvContent: csv, userId: 1);

        Assert.NotNull(response.Data);
        Assert.True(response.Data.IsValid);
        var row = response.Data.Rows[0];
        Assert.NotNull(row.Notes);
        Assert.StartsWith("'", row.Notes); // Neutralized with leading quote
    }

    [Fact]
    public async Task ValidateCsv_FutureDate_ReportsDateError()
    {
        using var context = CreateInMemoryDbContext();
        var service = new CsvTransactionImportService(context);

        var futureDate = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-dd");
        var csv = $@"transaction_date,company_ticker,transaction_type,quantity,price_per_share
{futureDate},GP,BUY,100,410";

        var response = await service.ValidateCsvAsync(portfolioId: 1, csvContent: csv, userId: 1);

        Assert.NotNull(response.Data);
        Assert.False(response.Data.IsValid);
        Assert.Contains(response.Data.Rows[0].Errors, e => e.Field == "transaction_date" && e.Problem.Contains("future"));
    }

    [Fact]
    public async Task ExecuteCsvImport_AtomicMode_FailsWhenErrorsExistAndDatabaseUnchanged()
    {
        using var context = CreateInMemoryDbContext();
        var service = new CsvTransactionImportService(context);

        var initialCount = await context.Transactions.CountAsync();

        // 1 valid row, 1 invalid row (unknown ticker)
        var csv = @"transaction_date,company_ticker,transaction_type,quantity,price_per_share
2026-10-01,GP,BUY,100,410
2026-10-02,INVALID_STOCK,BUY,50,200";

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ExecuteCsvImportAsync(portfolioId: 1, csvContent: csv, allowPartialImport: false, userId: 1));

        Assert.Contains("Atomic import rejected", ex.Message);

        // Verify database left completely unchanged
        var finalCount = await context.Transactions.CountAsync();
        Assert.Equal(initialCount, finalCount);
    }

    [Fact]
    public async Task ExecuteCsvImport_PartialMode_ImportsValidRowsAndSkipsInvalid()
    {
        using var context = CreateInMemoryDbContext();
        var service = new CsvTransactionImportService(context);

        // 1 valid row, 1 invalid row (negative quantity)
        var csv = @"transaction_date,company_ticker,transaction_type,quantity,price_per_share,notes
2026-10-01,GP,BUY,100,410,Valid Row
2026-10-02,BATBC,BUY,-20,520,Invalid Row";

        var response = await service.ExecuteCsvImportAsync(portfolioId: 1, csvContent: csv, allowPartialImport: true, userId: 1);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(1, response.Data.SuccessCount);
        Assert.Equal(1, response.Data.SkippedCount);
        Assert.Equal("Partial", response.Data.ImportMode);
        Assert.Single(response.Data.ImportedTransactions);
        Assert.Single(response.Data.SkippedRows);

        // Verify valid row in DB
        var tx = await context.Transactions.FirstOrDefaultAsync(t => t.Quantity == 100);
        Assert.NotNull(tx);
        Assert.Equal(100, tx.Quantity);
    }

    [Fact]
    public async Task ExecuteCsvImport_ValidCsv_InsertsRecordsAndGeneratesAuditTrail()
    {
        using var context = CreateInMemoryDbContext();
        var service = new CsvTransactionImportService(context);

        var csv = @"transaction_date,company_ticker,transaction_type,quantity,price_per_share,notes
2026-10-01,GP,BUY,100,410.00,Batch Import 1
2026-10-02,GP,SELL,30,420.00,Batch Import 2";

        var response = await service.ExecuteCsvImportAsync(portfolioId: 1, csvContent: csv, allowPartialImport: false, userId: 1);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(2, response.Data.SuccessCount);
        Assert.Equal(0, response.Data.SkippedCount);
        Assert.Equal(100 * 410m + 30 * 420m, response.Data.TotalAmount);

        // Verify transactions exist in DB
        var txs = await context.Transactions.Where(t => t.PortfolioId == 1).ToListAsync();
        Assert.Equal(2, txs.Count);

        // Verify audit trail generated for each transaction
        var audits = await context.TransactionAudits.ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.All(audits, a =>
        {
            Assert.Equal("INSERT", a.ActionType);
            Assert.Contains("CSV Import", a.ChangedBy);
        });
    }

    [Fact]
    public async Task ExecuteCsvImport_UserIsolation_RejectsImportToUnownedPortfolio()
    {
        using var context = CreateInMemoryDbContext();
        var service = new CsvTransactionImportService(context);

        // Portfolio 3 belongs to User 2. User 1 attempts import.
        var csv = @"transaction_date,company_ticker,transaction_type,quantity,price_per_share
2026-10-01,GP,BUY,100,410";

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.ExecuteCsvImportAsync(portfolioId: 3, csvContent: csv, allowPartialImport: false, userId: 1));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public void GenerateSampleCsvTemplate_ReturnsValidCsv()
    {
        using var context = CreateInMemoryDbContext();
        var service = new CsvTransactionImportService(context);

        var template = service.GenerateSampleCsvTemplate();

        Assert.False(string.IsNullOrWhiteSpace(template));
        Assert.Contains("transaction_date", template);
        Assert.Contains("company_ticker", template);
        Assert.Contains("transaction_type", template);
        Assert.Contains("quantity", template);
        Assert.Contains("price_per_share", template);
        Assert.Contains("GP", template);
    }
}
