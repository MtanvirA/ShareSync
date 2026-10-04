using Microsoft.EntityFrameworkCore;
using ShareSync.Application.DTOs.Dashboard;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class DashboardTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new ShareSyncDbContext(options);

        // Seed Users
        context.Users.AddRange(
            new AppUser { UserId = 1, Email = "user1@sharesync.com", Name = "Tanvir Ahmed", PasswordHash = "hash1" },
            new AppUser { UserId = 2, Email = "user2@sharesync.com", Name = "Rahim Khan", PasswordHash = "hash2" },
            new AppUser { UserId = 3, Email = "empty@sharesync.com", Name = "Empty User", PasswordHash = "hash3" }
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
            new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "User 1 Growth" },
            new Portfolio { PortfolioId = 2, UserId = 1, PortfolioName = "User 1 Income" },
            new Portfolio { PortfolioId = 3, UserId = 2, PortfolioName = "User 2 Main" }
        );

        // Seed Transactions for User 1:
        // Portfolio 1:
        // GP: BUY 100 @ 280 (cost 28,000)
        // SQUR: BUY 50 @ 200 (cost 10,000)
        // Portfolio 2:
        // BX: BUY 200 @ 100 (cost 20,000)
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
                CompanyId = 2,
                TransactionType = "BUY",
                Quantity = 50,
                PricePerShare = 200m,
                TransactionDate = new DateTime(2026, 2, 1)
            },
            new Transaction
            {
                TransactionId = 3,
                PortfolioId = 2,
                CompanyId = 3,
                TransactionType = "BUY",
                Quantity = 200,
                PricePerShare = 100m,
                TransactionDate = new DateTime(2026, 3, 5)
            }
        );

        // Seed Transactions for User 2:
        context.Transactions.AddRange(
            new Transaction
            {
                TransactionId = 4,
                PortfolioId = 3,
                CompanyId = 1,
                TransactionType = "BUY",
                Quantity = 500,
                PricePerShare = 250m,
                TransactionDate = new DateTime(2026, 1, 1)
            }
        );

        // Seed Dividends
        context.Dividends.AddRange(
            new Dividend
            {
                DividendId = 1,
                CompanyId = 1,
                DividendPerShare = 10.00m,
                DeclarationDate = new DateTime(2026, 4, 1),
                PaymentDate = new DateTime(2026, 4, 15)
            },
            new Dividend
            {
                DividendId = 2,
                CompanyId = 3,
                DividendPerShare = 5.00m,
                DeclarationDate = new DateTime(2026, 4, 1),
                PaymentDate = new DateTime(2026, 4, 20)
            }
        );



        // Seed Snapshots for Portfolio 1
        var now = DateTime.UtcNow.Date;
        context.PortfolioSnapshots.AddRange(
            new PortfolioSnapshot
            {
                SnapshotId = 1,
                PortfolioId = 1,
                TotalValue = 35000m,
                SnapshotDate = now.AddMonths(-2)
            },
            new PortfolioSnapshot
            {
                SnapshotId = 2,
                PortfolioId = 1,
                TotalValue = 41000m,
                SnapshotDate = now.AddMonths(-1)
            }
        );

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task GetDashboardData_EmptyUser_ReturnsZeroMetricsAndEmptyLists()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var reportService = new ReportService(context);
        var dashboardService = new DashboardService(context, reportService);

        // Act (User 3 has no portfolios or transactions)
        var response = await dashboardService.GetDashboardDataAsync(userId: 3);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);

        var data = response.Data;
        Assert.NotNull(data);
        Assert.Equal("Empty User", data.UserFullName);
        Assert.Equal(0, data.Summary.PortfoliosCount);
        Assert.Equal(0, data.Summary.HoldingsCount);
        Assert.Equal(0m, data.Summary.TotalPortfolioValue);
        Assert.Equal(0m, data.Summary.TotalInvested);
        Assert.Equal(0m, data.Summary.TotalUnrealizedProfitLoss);
        Assert.Equal(0m, data.Summary.TotalDividendIncome);
        Assert.Empty(data.RecentTransactions);
        Assert.Empty(data.TopHoldings);
        Assert.Empty(data.SectorAllocation);
    }

    [Fact]
    public async Task GetDashboardData_UserWithMultiplePortfolios_AggregatesAcrossAllPortfolios()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var reportService = new ReportService(context);
        var dashboardService = new DashboardService(context, reportService);

        // Act (User 1 has 2 portfolios: Portfolio 1 and Portfolio 2)
        var response = await dashboardService.GetDashboardDataAsync(userId: 1);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);

        var data = response.Data;
        Assert.Equal("Tanvir Ahmed", data.UserFullName);
        Assert.Equal(2, data.Summary.PortfoliosCount);
        Assert.Equal(3, data.Summary.HoldingsCount); // GP, SQUR, BX

        // Holdings values:
        // GP: 100 * 300 = 30,000 (Cost = 28,000)
        // SQUR: 50 * 220 = 11,000 (Cost = 10,000)
        // BX: 200 * 120 = 24,000 (Cost = 20,000)
        // Total Market Value = 30,000 + 11,000 + 24,000 = 65,000
        // Total Invested = 28,000 + 10,000 + 20,000 = 58,000
        // Unrealized P/L = 65,000 - 58,000 = 7,000
        Assert.Equal(65000m, data.Summary.TotalPortfolioValue);
        Assert.Equal(58000m, data.Summary.TotalInvested);
        Assert.Equal(7000m, data.Summary.TotalUnrealizedProfitLoss);

        // Dividends for User 1:
        // GP dividend = 10 * 100 = 1000
        // BX dividend = 5 * 200 = 1000
        // Total = 2000
        Assert.Equal(2000m, data.Summary.TotalDividendIncome);

        // Top holdings should be ordered by MarketValue descending
        Assert.Equal(3, data.TopHoldings.Count);
        Assert.Equal("GP", data.TopHoldings[0].TickerSymbol); // 30,000
        Assert.Equal("BX", data.TopHoldings[1].TickerSymbol); // 24,000
        Assert.Equal("SQURPHARMA", data.TopHoldings[2].TickerSymbol); // 11,000

        // Recent transactions should have 3 transactions for user 1
        Assert.Equal(3, data.RecentTransactions.Count);

        // Sector allocation should cover 3 sectors
        Assert.Equal(3, data.SectorAllocation.Count);
    }

    [Fact]
    public async Task GetDashboardData_FilteredByPortfolio_CalculatesOnlyForTargetPortfolio()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var reportService = new ReportService(context);
        var dashboardService = new DashboardService(context, reportService);

        // Act (Filter to Portfolio 1 only)
        var response = await dashboardService.GetDashboardDataAsync(userId: 1, portfolioId: 1);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);

        var data = response.Data;
        Assert.Equal(2, data.Summary.HoldingsCount); // GP and SQUR only
        Assert.Equal(41000m, data.Summary.TotalPortfolioValue); // 30,000 + 11,000
        Assert.Equal(38000m, data.Summary.TotalInvested); // 28,000 + 10,000
        Assert.Equal(3000m, data.Summary.TotalUnrealizedProfitLoss); // 3,000

        Assert.Equal(2, data.TopHoldings.Count);
        Assert.All(data.RecentTransactions, t => Assert.Equal(1, t.PortfolioId));
    }

    [Fact]
    public async Task GetDashboardData_MatchesReportSummaryNumbers_ExactParity()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var reportService = new ReportService(context);
        var dashboardService = new DashboardService(context, reportService);

        // Act
        var dashboardRes = await dashboardService.GetDashboardDataAsync(userId: 1);
        var reportSummaryRes = await reportService.GetReportSummaryAsync(userId: 1);
        var holdingsReportRes = await reportService.GetHoldingsReportAsync(userId: 1);

        // Assert: 100% agreement between Dashboard and Reports
        Assert.True(dashboardRes.Success);
        Assert.True(reportSummaryRes.Success);
        Assert.True(holdingsReportRes.Success);

        var d = dashboardRes.Data!.Summary;
        var r = reportSummaryRes.Data!;
        var h = holdingsReportRes.Data!;

        Assert.Equal(r.TotalPortfolioValue, d.TotalPortfolioValue);
        Assert.Equal(r.TotalUnrealizedProfitLoss, d.TotalUnrealizedProfitLoss);
        Assert.Equal(r.TotalDividendIncome, d.TotalDividendIncome);

        Assert.Equal(h.TotalMarketValue, d.TotalPortfolioValue);
        Assert.Equal(h.TotalInvested, d.TotalInvested);
        Assert.Equal(h.TotalUnrealizedProfitLoss, d.TotalUnrealizedProfitLoss);
        Assert.Equal(h.TotalProfitLossPercentage, d.ProfitLossPercentage);
        Assert.Equal(h.Holdings.Count, d.HoldingsCount);
    }

    [Fact]
    public async Task GetDashboardData_DataIsolation_User1NeverSeesUser2Data()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var reportService = new ReportService(context);
        var dashboardService = new DashboardService(context, reportService);

        // Act: fetch User 2
        var user2Res = await dashboardService.GetDashboardDataAsync(userId: 2);
        var user1Res = await dashboardService.GetDashboardDataAsync(userId: 1);

        // Assert: User 2 has 1 portfolio with 500 GP shares @ 300 = 150,000
        Assert.Equal(1, user2Res.Data.Summary.PortfoliosCount);
        Assert.Equal(1, user2Res.Data.Summary.HoldingsCount);
        Assert.Equal(150000m, user2Res.Data.Summary.TotalPortfolioValue);
        Assert.Equal(125000m, user2Res.Data.Summary.TotalInvested);

        // User 1 has 65,000 market value and 2 portfolios
        Assert.Equal(65000m, user1Res.Data.Summary.TotalPortfolioValue);
        Assert.Equal(2, user1Res.Data.Summary.PortfoliosCount);

        // No transactions of User 2 appear in User 1's recent transactions
        var user1TxIds = user1Res.Data.RecentTransactions.Select(t => t.TransactionId).ToList();
        Assert.DoesNotContain(4, user1TxIds); // Transaction 4 belongs to User 2

        // User 1 cannot access User 2's portfolio by passing portfolioId = 3 (throws 403 Forbidden)
        var ex = await Assert.ThrowsAsync<ShareSync.Application.Common.Exceptions.AppException>(() =>
            dashboardService.GetDashboardDataAsync(userId: 1, portfolioId: 3));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task GetDashboardData_PerformanceChart_PopulatesSnapshotsAndCalculatesChanges()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var reportService = new ReportService(context);
        var dashboardService = new DashboardService(context, reportService);

        // Act
        var response = await dashboardService.GetDashboardDataAsync(userId: 1, portfolioId: 1, period: "6m");

        // Assert
        Assert.True(response.Success);
        var perf = response.Data.Performance;
        Assert.NotNull(perf);
        Assert.Equal(35000m, perf.StartingValue);
        Assert.Equal(41000m, perf.CurrentValue);
        Assert.Equal(6000m, perf.NetChange);
        Assert.True(perf.Labels.Count >= 2);
        Assert.True(perf.Values.Count >= 2);
    }
}
