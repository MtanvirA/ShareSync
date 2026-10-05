using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class PortfolioAnalyticsTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new ShareSyncDbContext(options);

        // Seed Users
        context.Users.AddRange(
            new AppUser { UserId = 1, Email = "analyst1@sharesync.com", Name = "Analyst One", PasswordHash = "hash1" },
            new AppUser { UserId = 2, Email = "analyst2@sharesync.com", Name = "Analyst Two", PasswordHash = "hash2" },
            new AppUser { UserId = 3, Email = "emptyuser@sharesync.com", Name = "Empty User", PasswordHash = "hash3" }
        );

        // Seed Sectors
        context.Sectors.AddRange(
            new Sector { SectorId = 1, SectorName = "Telecommunications" },
            new Sector { SectorId = 2, SectorName = "Pharmaceuticals" },
            new Sector { SectorId = 3, SectorName = "Banking" },
            new Sector { SectorId = 4, SectorName = "IT" }
        );

        // Seed Companies
        context.Companies.AddRange(
            new Company { CompanyId = 1, TickerSymbol = "GP", CompanyName = "Grameenphone Ltd", CurrentPrice = 300.00m, SectorId = 1 },
            new Company { CompanyId = 2, TickerSymbol = "SQURPHARMA", CompanyName = "Square Pharmaceuticals", CurrentPrice = 250.00m, SectorId = 2 },
            new Company { CompanyId = 3, TickerSymbol = "BRACBANK", CompanyName = "BRAC Bank Ltd", CurrentPrice = 45.00m, SectorId = 3 },
            new Company { CompanyId = 4, TickerSymbol = "BATBC", CompanyName = "British American Tobacco", CurrentPrice = 500.00m, SectorId = 1 },
            new Company { CompanyId = 5, TickerSymbol = "RENATA", CompanyName = "Renata Ltd", CurrentPrice = 80.00m, SectorId = 2 }
        );

        // Seed Portfolios
        // User 1: Portfolio 1 (Multi-holding, multi-sector), Portfolio 2 (Single holding), Portfolio 3 (Empty)
        // User 2: Portfolio 4 (Owned by User 2)
        context.Portfolios.AddRange(
            new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "Core Growth" },
            new Portfolio { PortfolioId = 2, UserId = 1, PortfolioName = "Telecom Only" },
            new Portfolio { PortfolioId = 3, UserId = 1, PortfolioName = "Cash Reserve Empty" },
            new Portfolio { PortfolioId = 4, UserId = 2, PortfolioName = "User 2 Fund" }
        );

        // Seed Transactions for Portfolio 1:
        // GP: BUY 100 @ 250 (cost 25000), BUY 50 @ 280 (cost 14000), Total bought 150 @ avg 260.
        //     SELL 30 @ 310 (realized profit = 30 * (310 - 260) = +1500). Remaining 120 shares.
        //     CurrentPrice = 300. Market Value = 120 * 300 = 36,000. Invested = 120 * 260 = 31,200. Unrealized P/L = +4,800.
        // SQURPHARMA: BUY 100 @ 200 (cost 20000). Remaining 100 shares.
        //     CurrentPrice = 250. Market Value = 100 * 250 = 25,000. Invested = 20,000. Unrealized P/L = +5,000.
        // BRACBANK: BUY 200 @ 50 (cost 10000). Remaining 200 shares.
        //     CurrentPrice = 45 (Losing stock). Market Value = 200 * 45 = 9,000. Invested = 10,000. Unrealized P/L = -1,000.
        context.Transactions.AddRange(
            new Transaction
            {
                TransactionId = 1,
                PortfolioId = 1,
                CompanyId = 1,
                TransactionType = "BUY",
                Quantity = 100,
                PricePerShare = 250.00m,
                TransactionDate = new DateTime(2026, 1, 5)
            },
            new Transaction
            {
                TransactionId = 2,
                PortfolioId = 1,
                CompanyId = 1,
                TransactionType = "BUY",
                Quantity = 50,
                PricePerShare = 280.00m,
                TransactionDate = new DateTime(2026, 1, 15)
            },
            new Transaction
            {
                TransactionId = 3,
                PortfolioId = 1,
                CompanyId = 1,
                TransactionType = "SELL",
                Quantity = 30,
                PricePerShare = 310.00m,
                TransactionDate = new DateTime(2026, 2, 1)
            },
            new Transaction
            {
                TransactionId = 4,
                PortfolioId = 1,
                CompanyId = 2,
                TransactionType = "BUY",
                Quantity = 100,
                PricePerShare = 200.00m,
                TransactionDate = new DateTime(2026, 2, 10)
            },
            new Transaction
            {
                TransactionId = 5,
                PortfolioId = 1,
                CompanyId = 3,
                TransactionType = "BUY",
                Quantity = 200,
                PricePerShare = 50.00m,
                TransactionDate = new DateTime(2026, 2, 20)
            }
        );

        // Seed Transactions for Portfolio 2: Single holding (BATBC)
        // BUY 10 @ 400 (cost 4000). CurrentPrice = 500. Market Value = 5000.
        context.Transactions.Add(new Transaction
        {
            TransactionId = 6,
            PortfolioId = 2,
            CompanyId = 4,
            TransactionType = "BUY",
            Quantity = 10,
            PricePerShare = 400.00m,
            TransactionDate = new DateTime(2026, 1, 12)
        });

        // Seed Transactions for User 2 (Portfolio 4)
        context.Transactions.Add(new Transaction
        {
            TransactionId = 7,
            PortfolioId = 4,
            CompanyId = 5,
            TransactionType = "BUY",
            Quantity = 50,
            PricePerShare = 90.00m,
            TransactionDate = new DateTime(2026, 1, 15)
        });

        // Seed Dividends
        // GP paid 10.00 per share on 2026-02-15 (after User 1 bought 120 shares net)
        // Expected GP dividend = 120 * 10 = 1,200.00
        context.Dividends.Add(new Dividend
        {
            DividendId = 1,
            CompanyId = 1,
            DividendPerShare = 10.00m,
            DeclarationDate = new DateTime(2026, 2, 1),
            PaymentDate = new DateTime(2026, 2, 15)
        });

        // Seed Snapshots for Portfolio 1
        context.PortfolioSnapshots.AddRange(
            new PortfolioSnapshot
            {
                SnapshotId = 1,
                PortfolioId = 1,
                SnapshotDate = DateTime.UtcNow.AddMonths(-4),
                TotalValue = 50000m
            },
            new PortfolioSnapshot
            {
                SnapshotId = 2,
                PortfolioId = 1,
                SnapshotDate = DateTime.UtcNow.AddMonths(-2),
                TotalValue = 62000m
            },
            new PortfolioSnapshot
            {
                SnapshotId = 3,
                PortfolioId = 1,
                SnapshotDate = DateTime.UtcNow.AddDays(-10),
                TotalValue = 70000m
            }
        );

        context.SaveChanges();
        return context;
    }

    private AnalyticsService CreateService(ShareSyncDbContext context)
    {
        var reportService = new ReportService(context);
        var dividendService = new DividendService(context);
        return new AnalyticsService(context, reportService, dividendService);
    }

    [Fact]
    public async Task GetAnalytics_EmptyPortfolio_ReturnsZeroMetricsAndEmptyHoldings()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);

        var result = await service.GetAnalyticsAsync(userId: 1, portfolioId: 3);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var data = result.Data;
        Assert.Equal(0, data.TotalHoldingsCount);
        Assert.Equal(0, data.PortfolioValue);
        Assert.Equal(0, data.InvestedCapital);
        Assert.Equal(0, data.UnrealizedProfitLoss);
        Assert.Equal(0, data.UnrealizedReturnPercentage);
        Assert.Equal(0, data.RealizedProfitLoss);
        Assert.Equal(0, data.DividendIncome);
        Assert.Equal(0, data.TotalReturn);
        Assert.Equal(0, data.TopHoldingWeight);
        Assert.Equal(0, data.LargestHoldingWeight);
        Assert.Equal(0, data.LargestSectorWeight);
        Assert.Equal(0, data.DiversificationScore);
        Assert.Equal("Empty Portfolio", data.ConcentrationStatus);
        Assert.Empty(data.SectorAllocation);
        Assert.Empty(data.CompanyAllocation);
        Assert.Empty(data.TopGainers);
        Assert.Empty(data.TopLosers);
        Assert.Contains("LargestHoldingPercentage", data.MetricFormulas.Keys);
        Assert.Contains("LargestSectorPercentage", data.MetricFormulas.Keys);
        Assert.Contains("DiversificationScore", data.MetricFormulas.Keys);
    }

    [Fact]
    public async Task GetAnalytics_UserWithNoPortfolios_ReturnsNoPortfoliosContext()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);

        var result = await service.GetAnalyticsAsync(userId: 3);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("No Portfolios", result.Data.PortfolioName);
        Assert.Equal(0, result.Data.PortfolioValue);
    }

    [Fact]
    public async Task GetAnalytics_SingleHolding_ReturnsHundredPercentAllocationAndMaxHHI()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);

        // Portfolio 2 has 10 BATBC shares @ 400 buy = 4,000 invested. Current price = 500 => Market value = 5,000.
        var result = await service.GetAnalyticsAsync(userId: 1, portfolioId: 2);

        Assert.NotNull(result?.Data);
        var data = result.Data;

        Assert.Equal(1, data.TotalHoldingsCount);
        Assert.Equal(5000.00m, data.PortfolioValue);
        Assert.Equal(4000.00m, data.InvestedCapital);
        Assert.Equal(1000.00m, data.UnrealizedProfitLoss);
        Assert.Equal(25.00m, data.UnrealizedReturnPercentage); // (1000 / 4000) * 100 = 25%

        // Allocation checks
        Assert.Single(data.CompanyAllocation);
        Assert.Equal(100.00m, data.CompanyAllocation[0].AllocationPercentage);
        Assert.Equal(100.00m, data.TopHoldingWeight);
        Assert.Equal(100.00m, data.LargestHoldingWeight);
        Assert.Equal(100.00m, data.LargestSectorWeight);
        Assert.Equal(100.00m, data.Top3Concentration);
        Assert.Equal(10000.00m, data.HerfindahlIndex); // 100^2 = 10000
        Assert.Equal(0m, data.DiversificationScore); // 100 - (10000 / 100) = 0%
        Assert.Equal("Highly Concentrated", data.ConcentrationStatus);

        // Sectors
        Assert.Single(data.SectorAllocation);
        Assert.Equal("Telecommunications", data.SectorAllocation[0].SectorName);
        Assert.Equal(100.00m, data.SectorAllocation[0].AllocationPercentage);
    }

    [Fact]
    public async Task GetAnalytics_MultipleHoldingsAndSectors_ReturnsAccurateMetrics()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);

        // Portfolio 1:
        // GP: 120 shares * 300 = 36,000 MV (cost: 120 * 260 = 31,200). Unrealized: +4,800 (+15.38%)
        // SQURPHARMA: 100 shares * 250 = 25,000 MV (cost: 20,000). Unrealized: +5,000 (+25.00%)
        // BRACBANK: 200 shares * 45 = 9,000 MV (cost: 10,000). Unrealized: -1,000 (-10.00%)
        // Total Market Value = 36000 + 25000 + 9000 = 70,000
        // Total Invested = 31200 + 20000 + 10000 = 61,200
        // Total Unrealized = 4800 + 5000 - 1000 = +8,800 (+14.38%)
        var result = await service.GetAnalyticsAsync(userId: 1, portfolioId: 1);

        Assert.NotNull(result?.Data);
        var data = result.Data;

        Assert.Equal(3, data.TotalHoldingsCount);
        Assert.Equal(70000.00m, data.PortfolioValue);
        Assert.Equal(61200.00m, data.InvestedCapital);
        Assert.Equal(8800.00m, data.UnrealizedProfitLoss);
        Assert.Equal(14.38m, data.UnrealizedReturnPercentage);

        // Allocation checks
        // GP = 36000 / 70000 * 100 = 51.43%
        // SQUR = 25000 / 70000 * 100 = 35.71%
        // BRAC = 9000 / 70000 * 100 = 12.86%
        Assert.Equal(3, data.CompanyAllocation.Count);
        Assert.Equal("GP", data.CompanyAllocation[0].TickerSymbol);
        Assert.Equal(51.43m, data.CompanyAllocation[0].AllocationPercentage);
        Assert.Equal("SQURPHARMA", data.CompanyAllocation[1].TickerSymbol);
        Assert.Equal(35.71m, data.CompanyAllocation[1].AllocationPercentage);
        Assert.Equal("BRACBANK", data.CompanyAllocation[2].TickerSymbol);
        Assert.Equal(12.86m, data.CompanyAllocation[2].AllocationPercentage);

        // Concentration & Diversification
        Assert.Equal(51.43m, data.TopHoldingWeight);
        Assert.Equal(51.43m, data.LargestHoldingWeight);
        Assert.Equal(51.43m, data.LargestSectorWeight);
        Assert.Equal(100.00m, data.Top3Concentration);
        // HHI = 51.43^2 + 35.71^2 + 12.86^2 = 2645.04 + 1275.20 + 165.38 = 4085.62
        Assert.True(data.HerfindahlIndex > 4000 && data.HerfindahlIndex < 4200);
        // Diversification Score = 100 - (4085.62 / 100) = 59.1%
        Assert.Equal(59.1m, data.DiversificationScore);
        Assert.Equal("Highly Concentrated", data.ConcentrationStatus); // > 2500

        // Sectors (Telecommunications: 51.43%, Pharmaceuticals: 35.71%, Banking: 12.86%)
        Assert.Equal(3, data.SectorAllocation.Count);
        Assert.Equal("Telecommunications", data.SectorAllocation[0].SectorName);
        Assert.Equal(51.43m, data.SectorAllocation[0].AllocationPercentage);

        // Formulas
        Assert.Contains("LargestHoldingPercentage", data.MetricFormulas.Keys);
        Assert.Contains("LargestSectorPercentage", data.MetricFormulas.Keys);
        Assert.Contains("DiversificationScore", data.MetricFormulas.Keys);
    }

    [Fact]
    public async Task GetAnalytics_TopGainersAndLosers_CorrectlySeparatesAndRanks()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);

        var result = await service.GetAnalyticsAsync(userId: 1, portfolioId: 1);
        var data = result.Data!;

        // Gainers: SQURPHARMA (+25%), GP (+15.38%)
        Assert.Equal(2, data.TopGainers.Count);
        Assert.Equal("SQURPHARMA", data.TopGainers[0].TickerSymbol);
        Assert.Equal(25.00m, data.TopGainers[0].ReturnPercentage);
        Assert.Equal("GP", data.TopGainers[1].TickerSymbol);
        Assert.Equal(15.38m, data.TopGainers[1].ReturnPercentage);

        // Losers: BRACBANK (-10%)
        Assert.Single(data.TopLosers);
        Assert.Equal("BRACBANK", data.TopLosers[0].TickerSymbol);
        Assert.Equal(-10.00m, data.TopLosers[0].ReturnPercentage);
    }

    [Fact]
    public async Task GetAnalytics_RealizedPL_ComputesFromSellTransactions()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);

        // In Portfolio 1:
        // GP had BUY 100 @ 250, BUY 50 @ 280 -> Avg Buy Price = 260.
        // Then SELL 30 @ 310 -> Realized P/L = 30 * (310 - 260) = 1,500.00.
        var result = await service.GetAnalyticsAsync(userId: 1, portfolioId: 1);
        var data = result.Data!;

        Assert.Equal(1500.00m, data.RealizedProfitLoss);

        // Total Return = Unrealized (8800) + Realized (1500) + Dividend (1200) = 11,500.00
        Assert.Equal(11500.00m, data.TotalReturn);
    }

    [Fact]
    public async Task GetAnalytics_DividendContribution_CalculatesIncomeAndYield()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);

        var result = await service.GetAnalyticsAsync(userId: 1, portfolioId: 1);
        var data = result.Data!;

        // 120 shares GP * 10 dividend = 1,200.00
        Assert.Equal(1200.00m, data.DividendIncome);
        // Dividend Yield = (1200 / 70000) * 100 = 1.71%
        Assert.Equal(1.71m, data.DividendYield);
    }

    [Fact]
    public async Task GetAnalytics_HistoricalPerformance_RespectsTimeframeFilters()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);

        // Portfolio 1 has 3 snapshots: -4 months, -2 months, -10 days
        // "ALL" should return all 3 snapshots
        var allResult = await service.GetAnalyticsAsync(userId: 1, portfolioId: 1, period: "ALL");
        Assert.Equal(3, allResult.Data!.HistoricalPerformance.Values.Count);

        // "1M" (last 30 days) should include the -10 days snapshot (1 item)
        var oneMonthResult = await service.GetAnalyticsAsync(userId: 1, portfolioId: 1, period: "1M");
        Assert.Single(oneMonthResult.Data!.HistoricalPerformance.Values);

        // "3M" (last 90 days) should include -2 months and -10 days (2 items)
        var threeMonthResult = await service.GetAnalyticsAsync(userId: 1, portfolioId: 1, period: "3M");
        Assert.Equal(2, threeMonthResult.Data!.HistoricalPerformance.Values.Count);
    }

    [Fact]
    public async Task GetAnalytics_UserIsolation_ThrowsForbidden_WhenAccessingOtherUserPortfolio()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);

        // User 1 attempting to access Portfolio 4 (which belongs to User 2)
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.GetAnalyticsAsync(userId: 1, portfolioId: 4));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task GetAnalytics_NonExistentPortfolio_ThrowsNotFound()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);

        // Accessing portfolio 9999 which does not exist in the database
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetAnalyticsAsync(userId: 1, portfolioId: 9999));
    }

    [Fact]
    public async Task GetAnalytics_FinancialConsistency_MatchesAuthoritativeReportService()
    {
        using var context = CreateInMemoryDbContext();
        var reportService = new ReportService(context);
        var dividendService = new DividendService(context);
        var analyticsService = new AnalyticsService(context, reportService, dividendService);

        var holdingsReport = await reportService.GetHoldingsReportAsync(userId: 1, portfolioId: 1);
        var analytics = await analyticsService.GetAnalyticsAsync(userId: 1, portfolioId: 1);

        // Parity verification between authoritative ReportService and AnalyticsService
        Assert.Equal(holdingsReport.Data!.TotalMarketValue, analytics.Data!.PortfolioValue);
        Assert.Equal(holdingsReport.Data!.TotalInvested, analytics.Data!.InvestedCapital);
        Assert.Equal(holdingsReport.Data!.TotalUnrealizedProfitLoss, analytics.Data!.UnrealizedProfitLoss);
        Assert.Equal(holdingsReport.Data!.TotalProfitLossPercentage, analytics.Data!.UnrealizedReturnPercentage);
        Assert.Equal(holdingsReport.Data!.Holdings.Count, analytics.Data!.TotalHoldingsCount);
    }

    [Fact]
    public async Task GetAnalytics_WebFeature3_PortfolioRiskAndDiversificationMetrics_AreCompleteAndExplainable()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);

        // Act
        var res = await service.GetAnalyticsAsync(userId: 1, portfolioId: 1);

        // Assert
        Assert.NotNull(res);
        Assert.True(res.Success);
        var data = res.Data!;

        // 1. Number of holdings
        Assert.Equal(3, data.TotalHoldingsCount);

        // 2. Largest holding percentage
        Assert.Equal(51.43m, data.LargestHoldingWeight);
        Assert.Equal(data.TopHoldingWeight, data.LargestHoldingWeight);

        // 3. Sector allocation
        Assert.NotEmpty(data.SectorAllocation);
        Assert.Equal(51.43m, data.LargestSectorWeight);
        Assert.Equal("Telecommunications", data.SectorAllocation[0].SectorName);
        Assert.Equal(51.43m, data.SectorAllocation[0].AllocationPercentage);

        // 4. Company allocation
        Assert.NotEmpty(data.CompanyAllocation);
        Assert.Equal("GP", data.CompanyAllocation[0].TickerSymbol);
        Assert.Equal(51.43m, data.CompanyAllocation[0].AllocationPercentage);

        // 5. Concentration indicators
        Assert.Equal(51.43m, data.TopHoldingWeight);
        Assert.Equal(100.00m, data.Top3Concentration);
        Assert.Equal("Highly Concentrated", data.ConcentrationStatus);
        Assert.True(data.HerfindahlIndex > 0);

        // 6. Diversification indicator/score
        Assert.True(data.DiversificationScore >= 0 && data.DiversificationScore <= 100);
        Assert.Equal(59.1m, data.DiversificationScore);

        // 7. Explicit formulas documented
        Assert.True(data.MetricFormulas.ContainsKey("LargestHoldingPercentage"));
        Assert.True(data.MetricFormulas.ContainsKey("LargestSectorPercentage"));
        Assert.True(data.MetricFormulas.ContainsKey("DiversificationScore"));
        Assert.True(data.MetricFormulas.ContainsKey("HerfindahlIndex"));
    }
}
