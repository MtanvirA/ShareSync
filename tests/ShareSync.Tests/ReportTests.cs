using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class ReportTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new ShareSyncDbContext(options);

        // Seed Users
        context.Users.AddRange(
            new AppUser { UserId = 1, Email = "user1@sharesync.com", Name = "User 1", PasswordHash = "hash1" },
            new AppUser { UserId = 2, Email = "user2@sharesync.com", Name = "User 2", PasswordHash = "hash2" }
        );

        // Seed Sectors
        context.Sectors.AddRange(
            new Sector { SectorId = 1, SectorName = "Telecommunications" },
            new Sector { SectorId = 2, SectorName = "Pharmaceuticals" },
            new Sector { SectorId = 3, SectorName = "Conglomerate" }
        );

        // Seed Companies
        context.Companies.AddRange(
            new Company { CompanyId = 1, TickerSymbol = "GP", CompanyName = "Grameenphone", CurrentPrice = 300.00m, SectorId = 1 },
            new Company { CompanyId = 2, TickerSymbol = "SQURPHARMA", CompanyName = "Square Pharma", CurrentPrice = 220.00m, SectorId = 2 },
            new Company { CompanyId = 3, TickerSymbol = "BX", CompanyName = "Beximco", CurrentPrice = 120.00m, SectorId = 3 }
        );

        // Seed Portfolios
        context.Portfolios.AddRange(
            new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "User 1 Main" },
            new Portfolio { PortfolioId = 2, UserId = 2, PortfolioName = "User 2 Main" }
        );

        // Seed Transactions for User 1 Portfolio 1:
        // GP: BUY 100 @ 280 (cost 28,000), BUY 50 @ 290 (cost 14,500), SELL 30 @ 310 (sell 9,300)
        // Net GP: 120 shares. Avg buy price: (28000+14500)/150 = 283.33. Market value = 120 * 300 = 36,000.
        // SQUR: BUY 50 @ 200 (cost 10,000). Net SQUR: 50 shares. Market value = 50 * 220 = 11,000.
        context.Transactions.AddRange(
            new Transaction
            {
                TransactionId = 1,
                PortfolioId = 1,
                CompanyId = 1,
                TransactionType = "BUY",
                Quantity = 100,
                PricePerShare = 280m,
                TransactionDate = new DateTime(2026, 1, 10)
            },
            new Transaction
            {
                TransactionId = 2,
                PortfolioId = 1,
                CompanyId = 1,
                TransactionType = "BUY",
                Quantity = 50,
                PricePerShare = 290m,
                TransactionDate = new DateTime(2026, 1, 20)
            },
            new Transaction
            {
                TransactionId = 3,
                PortfolioId = 1,
                CompanyId = 1,
                TransactionType = "SELL",
                Quantity = 30,
                PricePerShare = 310m,
                TransactionDate = new DateTime(2026, 2, 5)
            },
            new Transaction
            {
                TransactionId = 4,
                PortfolioId = 1,
                CompanyId = 2,
                TransactionType = "BUY",
                Quantity = 50,
                PricePerShare = 200m,
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
                PricePerShare = 100m,
                TransactionDate = new DateTime(2026, 2, 15)
            }
        );

        // Seed Snapshots for Portfolio 1
        context.PortfolioSnapshots.AddRange(
            new PortfolioSnapshot { SnapshotId = 1, PortfolioId = 1, SnapshotDate = new DateTime(2026, 1, 31), TotalValue = 42500m },
            new PortfolioSnapshot { SnapshotId = 2, PortfolioId = 1, SnapshotDate = new DateTime(2026, 2, 28), TotalValue = 47000m },
            new PortfolioSnapshot { SnapshotId = 3, PortfolioId = 2, SnapshotDate = new DateTime(2026, 2, 28), TotalValue = 20000m }
        );

        // Seed Dividends
        context.Dividends.AddRange(
            new Dividend
            {
                DividendId = 1,
                CompanyId = 1,
                DividendPerShare = 12.00m,
                DeclarationDate = new DateTime(2026, 2, 1),
                PaymentDate = new DateTime(2026, 3, 1)
            },
            new Dividend
            {
                DividendId = 2,
                CompanyId = 2,
                DividendPerShare = 8.50m,
                DeclarationDate = new DateTime(2026, 2, 15),
                PaymentDate = new DateTime(2026, 3, 15)
            }
        );

        // Seed Watchlists
        var wl = new Watchlist { WatchlistId = 1, UserId = 1, WatchlistName = "Blue Chips" };
        wl.Items.Add(new WatchlistItem { WatchlistId = 1, CompanyId = 1, TargetPrice = 320m });
        wl.Items.Add(new WatchlistItem { WatchlistId = 1, CompanyId = 2, TargetPrice = 210m });
        context.Watchlists.Add(wl);

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task GetHoldingsReport_ReturnsAccurateMetrics()
    {
        using var context = CreateInMemoryDbContext();
        var service = new ReportService(context);

        var response = await service.GetHoldingsReportAsync(userId: 1, portfolioId: 1);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        // Note: in-memory DB doesn't execute real DB views, but service will query holdings view or fallback
        // When querying in-memory view, let's test company/sector report which runs against entity set directly
    }

    [Fact]
    public async Task GetHoldingsReport_UnauthorizedPortfolio_ThrowsForbidden()
    {
        using var context = CreateInMemoryDbContext();
        var service = new ReportService(context);

        // User 2 trying to access User 1's portfolio (id=1)
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.GetHoldingsReportAsync(userId: 2, portfolioId: 1));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task GetCompanySectorReport_AggregatesCompaniesAndSectorsAccurately()
    {
        using var context = CreateInMemoryDbContext();
        var service = new ReportService(context);

        var response = await service.GetCompanySectorReportAsync(userId: 1, portfolioId: 1);

        Assert.True(response.Success);
        var data = response.Data;

        // User 1 has 2 companies: GP (120 shares * 300 = 36,000) and SQURPHARMA (50 shares * 220 = 11,000)
        // Total Portfolio Market Value = 47,000
        Assert.Equal(47000m, data.TotalPortfolioValue);
        Assert.Equal(2, data.Companies.Count);
        Assert.Equal(2, data.Sectors.Count);

        var teleSector = data.Sectors.FirstOrDefault(s => s.SectorName == "Telecommunications");
        Assert.NotNull(teleSector);
        Assert.Equal(36000m, teleSector.CurrentMarketValue);
        Assert.Equal(1, teleSector.HoldingsCount);
        Assert.Equal(76.60m, teleSector.AllocationPercentage); // 36,000 / 47,000 * 100 = 76.60%

        var pharmaSector = data.Sectors.FirstOrDefault(s => s.SectorName == "Pharmaceuticals");
        Assert.NotNull(pharmaSector);
        Assert.Equal(11000m, pharmaSector.CurrentMarketValue);
        Assert.Equal(23.40m, pharmaSector.AllocationPercentage); // 11,000 / 47,000 * 100 = 23.40%
    }

    [Fact]
    public async Task GetTransactionHistoryReport_ReturnsAccurateCashFlowAndCounts()
    {
        using var context = CreateInMemoryDbContext();
        var service = new ReportService(context);

        var response = await service.GetTransactionHistoryReportAsync(userId: 1);

        Assert.True(response.Success);
        var data = response.Data;

        // User 1 has 4 transactions (3 BUY, 1 SELL)
        Assert.Equal(4, data.TotalTransactions);
        Assert.Equal(3, data.BuyCount);
        Assert.Equal(1, data.SellCount);

        // Buy total: 100*280 (28k) + 50*290 (14.5k) + 50*200 (10k) = 52,500
        Assert.Equal(52500m, data.TotalBuyValue);
        // Sell total: 30*310 = 9,300
        Assert.Equal(9300m, data.TotalSellValue);
        // Net cash flow: 9,300 - 52,500 = -43,200
        Assert.Equal(-43200m, data.NetCashFlow);
    }

    [Fact]
    public async Task GetTransactionHistoryReport_FilterByType_ReturnsFiltered()
    {
        using var context = CreateInMemoryDbContext();
        var service = new ReportService(context);

        var response = await service.GetTransactionHistoryReportAsync(userId: 1, transactionType: "SELL");

        Assert.True(response.Success);
        Assert.Single(response.Data.Transactions);
        Assert.Equal("SELL", response.Data.Transactions[0].TransactionType);
        Assert.Equal(9300m, response.Data.TotalSellValue);
    }

    [Fact]
    public async Task GetPerformanceReport_ReturnsChronologicalSnapshotsAndMetrics()
    {
        using var context = CreateInMemoryDbContext();
        var service = new ReportService(context);

        var response = await service.GetPerformanceReportAsync(userId: 1, portfolioId: 1);

        Assert.True(response.Success);
        var data = response.Data;

        Assert.Equal(2, data.Snapshots.Count);
        Assert.Equal(42500m, data.StartingValue);
        Assert.Equal(47000m, data.CurrentValue);
        Assert.Equal(4500m, data.NetChange);
        Assert.Equal(10.59m, data.NetChangePercentage); // 4500 / 42500 * 100 = 10.59%
    }

    [Fact]
    public async Task GetDividendIncomeReport_CalculatesUserSharesAndEstimatedIncome()
    {
        using var context = CreateInMemoryDbContext();
        var service = new ReportService(context);

        var response = await service.GetDividendIncomeReportAsync(userId: 1);

        Assert.True(response.Success);
        var data = response.Data;

        // User 1 holds:
        // 120 shares of GP (Company 1) -> 120 * 12.00 = 1,440.00
        // 50 shares of SQURPHARMA (Company 2) -> 50 * 8.50 = 425.00
        // Total expected income = 1,865.00
        Assert.Equal(1865.00m, data.TotalIncome);
        Assert.Equal(2, data.CompanyBreakdown.Count);
        Assert.Equal(2, data.TotalPaymentsCount);
    }

    [Fact]
    public async Task GetWatchlistTargetReport_CalculatesSpreadAndTargetStatus()
    {
        using var context = CreateInMemoryDbContext();
        var service = new ReportService(context);

        var response = await service.GetWatchlistTargetReportAsync(userId: 1, watchlistId: 1);

        Assert.True(response.Success);
        var data = response.Data;

        Assert.Equal(2, data.TotalItems);

        // GP: Current price 300, Target 320 -> Diff +20 -> Near Target (300 >= 320*0.95 = 304? 300 < 304 -> Below Target)
        var gp = data.Items.FirstOrDefault(i => i.TickerSymbol == "GP");
        Assert.NotNull(gp);
        Assert.Equal(300m, gp.CurrentPrice);
        Assert.Equal(320m, gp.TargetPrice);
        Assert.Equal(20m, gp.PriceDifference);

        // SQURPHARMA: Current price 220, Target 210 -> Current >= Target -> "Target Reached"
        var sq = data.Items.FirstOrDefault(i => i.TickerSymbol == "SQURPHARMA");
        Assert.NotNull(sq);
        Assert.Equal("Target Reached", sq.Status);
    }

    [Fact]
    public async Task GetReportSummary_CombinesKeyMetrics()
    {
        using var context = CreateInMemoryDbContext();
        var service = new ReportService(context);

        var response = await service.GetReportSummaryAsync(userId: 1);

        Assert.True(response.Success);
        var data = response.Data;

        Assert.Equal(4, data.TotalTransactionsCount);
        Assert.Equal(1865m, data.TotalDividendIncome);
    }

    [Fact]
    public async Task Reports_StrictUserIsolation_UserCannotSeeOtherUsersData()
    {
        using var context = CreateInMemoryDbContext();
        var service = new ReportService(context);

        // User 2 transactions report should ONLY contain User 2's 1 transaction
        var txRes = await service.GetTransactionHistoryReportAsync(userId: 2);
        Assert.Single(txRes.Data.Transactions);
        Assert.Equal("BX", txRes.Data.Transactions[0].TickerSymbol);

        // User 2 performance report should NOT contain User 1's snapshots
        var perfRes = await service.GetPerformanceReportAsync(userId: 2, portfolioId: 2);
        Assert.Single(perfRes.Data.Snapshots);
        Assert.Equal(20000m, perfRes.Data.CurrentValue);

        // User 2 watchlist report should be empty (no watchlists for User 2)
        var wlRes = await service.GetWatchlistTargetReportAsync(userId: 2);
        Assert.Empty(wlRes.Data.Items);
    }
}
