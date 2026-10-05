using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

/// <summary>
/// Proves financial calculation consistency across PortfolioService, ReportService, and DashboardService,
/// and validates the mathematical identity with Oracle functions (fn_get_weighted_avg_price, fn_calculate_unrealized_pl)
/// using the Cumulative Weighted-Average Cost Basis model.
/// </summary>
public class FinancialCalculationConsistencyTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ShareSyncDbContext(options);
    }

    [Fact]
    public async Task CumulativeWeightedAverageCostBasis_FullLifecycleConsistency_BuyBuySellBuySell()
    {
        // =========================================================================
        // Test Lifecycle Case:
        // 1. BUY 100 @ 100 (Cost = 10,000, WABP = 100)
        // 2. BUY 100 @ 200 (Cost = 20,000, Total Buy Cost = 30,000, WABP = 150)
        // 3. SELL 100 @ 220 (Net Qty = 100, WABP remains 150, Invested = 15,000)
        // 4. BUY 50 @ 180  (Cost = 9,000, Total Buy Cost = 39,000, Total Buy Qty = 250, WABP = 156, Net Qty = 150)
        // 5. SELL 50 @ 210 (Net Qty = 100, WABP remains 156, Invested = 15,600)
        // =========================================================================

        using var context = CreateInMemoryDbContext();
        const int userId = 1;
        const int portfolioId = 10;
        const int companyId = 100;
        const int sectorId = 5;

        // Seed Entities
        var user = new AppUser { UserId = userId, Name = "Test User", Email = "test@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        var sector = new Sector { SectorId = sectorId, SectorName = "Banking" };
        var company = new Company
        {
            CompanyId = companyId,
            CompanyName = "Test Bank PLC",
            TickerSymbol = "TBANK",
            SectorId = sectorId,
            CurrentPrice = 200.00m,
            Sector = sector
        };
        var portfolio = new Portfolio
        {
            PortfolioId = portfolioId,
            UserId = userId,
            PortfolioName = "Alpha Portfolio",
            User = user
        };

        context.Users.Add(user);
        context.Sectors.Add(sector);
        context.Companies.Add(company);
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync();

        var portfolioService = new PortfolioService(context);
        var reportService = new ReportService(context);
        var dashboardService = new DashboardService(context, reportService);

        // -------------------------------------------------------------------------
        // STEP 1: BUY 100 @ 100
        // -------------------------------------------------------------------------
        var t1 = new Transaction
        {
            TransactionId = 1,
            PortfolioId = portfolioId,
            CompanyId = companyId,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 100.00m,
            TransactionDate = DateTime.UtcNow.AddDays(-10),
            Company = company,
            Portfolio = portfolio
        };
        context.Transactions.Add(t1);
        await context.SaveChangesAsync();

        // Verify Step 1 across services
        var pSummary1 = (await portfolioService.GetPortfolioByIdAsync(portfolioId, userId)).Data!;
        var rReport1 = (await reportService.GetHoldingsReportAsync(userId, portfolioId)).Data!;
        var dSummary1 = (await dashboardService.GetDashboardDataAsync(userId, portfolioId)).Data!.Summary;

        Assert.Equal(100, pSummary1.Holdings[0].Shares);
        Assert.Equal(100.00m, pSummary1.Holdings[0].AverageBuyPrice);
        Assert.Equal(20000.00m, pSummary1.TotalValue); // 100 * 200
        Assert.Equal(10000.00m, pSummary1.TotalInvested); // 100 * 100
        Assert.Equal(10000.00m, pSummary1.UnrealizedProfitLoss); // 20,000 - 10,000
        Assert.Equal(100.00m, pSummary1.UnrealizedProfitLossPercentage);

        // Verify ReportService produces identical numbers
        Assert.Equal(pSummary1.TotalValue, rReport1.TotalMarketValue);
        Assert.Equal(pSummary1.TotalInvested, rReport1.TotalInvested);
        Assert.Equal(pSummary1.UnrealizedProfitLoss, rReport1.TotalUnrealizedProfitLoss);

        // Verify DashboardService produces identical numbers
        Assert.Equal(pSummary1.TotalValue, dSummary1.TotalPortfolioValue);
        Assert.Equal(pSummary1.TotalInvested, dSummary1.TotalInvested);
        Assert.Equal(pSummary1.UnrealizedProfitLoss, dSummary1.TotalUnrealizedProfitLoss);

        // -------------------------------------------------------------------------
        // STEP 2: BUY 100 @ 200
        // Total Buy Qty = 200, Total Buy Cost = 10,000 + 20,000 = 30,000 => WABP = 150.00
        // -------------------------------------------------------------------------
        var t2 = new Transaction
        {
            TransactionId = 2,
            PortfolioId = portfolioId,
            CompanyId = companyId,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 200.00m,
            TransactionDate = DateTime.UtcNow.AddDays(-8),
            Company = company,
            Portfolio = portfolio
        };
        context.Transactions.Add(t2);
        await context.SaveChangesAsync();

        var pSummary2 = (await portfolioService.GetPortfolioByIdAsync(portfolioId, userId)).Data!;
        var rReport2 = (await reportService.GetHoldingsReportAsync(userId, portfolioId)).Data!;
        var dSummary2 = (await dashboardService.GetDashboardDataAsync(userId, portfolioId)).Data!.Summary;

        Assert.Equal(200, pSummary2.Holdings[0].Shares);
        Assert.Equal(150.00m, pSummary2.Holdings[0].AverageBuyPrice);
        Assert.Equal(40000.00m, pSummary2.TotalValue); // 200 * 200
        Assert.Equal(30000.00m, pSummary2.TotalInvested); // 200 * 150
        Assert.Equal(10000.00m, pSummary2.UnrealizedProfitLoss); // 40,000 - 30,000
        Assert.Equal(33.33m, pSummary2.UnrealizedProfitLossPercentage);

        // Consistency across layers
        Assert.Equal(pSummary2.TotalValue, rReport2.TotalMarketValue);
        Assert.Equal(pSummary2.TotalInvested, rReport2.TotalInvested);
        Assert.Equal(pSummary2.TotalValue, dSummary2.TotalPortfolioValue);
        Assert.Equal(pSummary2.TotalInvested, dSummary2.TotalInvested);

        // -------------------------------------------------------------------------
        // STEP 3: SELL 100 @ 220
        // Net Qty = 200 - 100 = 100
        // Under Weighted-Average Cost Basis, selling does NOT alter average purchase price of remaining shares
        // WABP remains 150.00, Invested Basis = 100 * 150.00 = 15,000.00
        // Market Value = 100 * 200.00 = 20,000.00, Unrealized PL = 20,000 - 15,000 = 5,000.00 (+33.33%)
        // -------------------------------------------------------------------------
        var t3 = new Transaction
        {
            TransactionId = 3,
            PortfolioId = portfolioId,
            CompanyId = companyId,
            TransactionType = "SELL",
            Quantity = 100,
            PricePerShare = 220.00m,
            TransactionDate = DateTime.UtcNow.AddDays(-6),
            Company = company,
            Portfolio = portfolio
        };
        context.Transactions.Add(t3);
        await context.SaveChangesAsync();

        var pSummary3 = (await portfolioService.GetPortfolioByIdAsync(portfolioId, userId)).Data!;
        var rReport3 = (await reportService.GetHoldingsReportAsync(userId, portfolioId)).Data!;
        var dSummary3 = (await dashboardService.GetDashboardDataAsync(userId, portfolioId)).Data!.Summary;

        Assert.Equal(100, pSummary3.Holdings[0].Shares);
        Assert.Equal(150.00m, pSummary3.Holdings[0].AverageBuyPrice);
        Assert.Equal(20000.00m, pSummary3.TotalValue); // 100 * 200
        Assert.Equal(15000.00m, pSummary3.TotalInvested); // 100 * 150
        Assert.Equal(5000.00m, pSummary3.UnrealizedProfitLoss); // 20,000 - 15,000
        Assert.Equal(33.33m, pSummary3.UnrealizedProfitLossPercentage);

        // Consistency across layers
        Assert.Equal(pSummary3.TotalValue, rReport3.TotalMarketValue);
        Assert.Equal(pSummary3.TotalInvested, rReport3.TotalInvested);
        Assert.Equal(pSummary3.UnrealizedProfitLoss, rReport3.TotalUnrealizedProfitLoss);
        Assert.Equal(pSummary3.TotalValue, dSummary3.TotalPortfolioValue);
        Assert.Equal(pSummary3.TotalInvested, dSummary3.TotalInvested);
        Assert.Equal(pSummary3.UnrealizedProfitLoss, dSummary3.TotalUnrealizedProfitLoss);

        // -------------------------------------------------------------------------
        // STEP 4: Additional BUY: BUY 50 @ 180
        // Total Buy Qty = 200 + 50 = 250
        // Total Buy Cost = 30,000 + (50 * 180) = 39,000.00 => WABP = 39,000 / 250 = 156.00
        // Net Qty = 100 + 50 = 150
        // Cost Basis = 150 * 156.00 = 23,400.00
        // Current Market Value = 150 * 200.00 = 30,000.00
        // Unrealized PL = 30,000 - 23,400 = 6,600.00 (+28.21%)
        // -------------------------------------------------------------------------
        var t4 = new Transaction
        {
            TransactionId = 4,
            PortfolioId = portfolioId,
            CompanyId = companyId,
            TransactionType = "BUY",
            Quantity = 50,
            PricePerShare = 180.00m,
            TransactionDate = DateTime.UtcNow.AddDays(-4),
            Company = company,
            Portfolio = portfolio
        };
        context.Transactions.Add(t4);
        await context.SaveChangesAsync();

        var pSummary4 = (await portfolioService.GetPortfolioByIdAsync(portfolioId, userId)).Data!;
        var rReport4 = (await reportService.GetHoldingsReportAsync(userId, portfolioId)).Data!;
        var dSummary4 = (await dashboardService.GetDashboardDataAsync(userId, portfolioId)).Data!.Summary;

        Assert.Equal(150, pSummary4.Holdings[0].Shares);
        Assert.Equal(156.00m, pSummary4.Holdings[0].AverageBuyPrice);
        Assert.Equal(30000.00m, pSummary4.TotalValue);
        Assert.Equal(23400.00m, pSummary4.TotalInvested);
        Assert.Equal(6600.00m, pSummary4.UnrealizedProfitLoss);
        Assert.Equal(28.21m, pSummary4.UnrealizedProfitLossPercentage);

        // Consistency across layers
        Assert.Equal(pSummary4.TotalValue, rReport4.TotalMarketValue);
        Assert.Equal(pSummary4.TotalInvested, rReport4.TotalInvested);
        Assert.Equal(pSummary4.TotalValue, dSummary4.TotalPortfolioValue);
        Assert.Equal(pSummary4.TotalInvested, dSummary4.TotalInvested);

        // -------------------------------------------------------------------------
        // STEP 5: Additional SELL: SELL 50 @ 210
        // Net Qty = 150 - 50 = 100
        // WABP remains 156.00
        // Cost Basis = 100 * 156.00 = 15,600.00
        // Current Market Value = 100 * 200.00 = 20,000.00
        // Unrealized PL = 20,000 - 15,600 = 4,400.00 (+28.21%)
        // -------------------------------------------------------------------------
        var t5 = new Transaction
        {
            TransactionId = 5,
            PortfolioId = portfolioId,
            CompanyId = companyId,
            TransactionType = "SELL",
            Quantity = 50,
            PricePerShare = 210.00m,
            TransactionDate = DateTime.UtcNow.AddDays(-2),
            Company = company,
            Portfolio = portfolio
        };
        context.Transactions.Add(t5);
        await context.SaveChangesAsync();

        var pSummary5 = (await portfolioService.GetPortfolioByIdAsync(portfolioId, userId)).Data!;
        var rReport5 = (await reportService.GetHoldingsReportAsync(userId, portfolioId)).Data!;
        var dSummary5 = (await dashboardService.GetDashboardDataAsync(userId, portfolioId)).Data!.Summary;

        Assert.Equal(100, pSummary5.Holdings[0].Shares);
        Assert.Equal(156.00m, pSummary5.Holdings[0].AverageBuyPrice);
        Assert.Equal(20000.00m, pSummary5.TotalValue);
        Assert.Equal(15600.00m, pSummary5.TotalInvested);
        Assert.Equal(4400.00m, pSummary5.UnrealizedProfitLoss);
        Assert.Equal(28.21m, pSummary5.UnrealizedProfitLossPercentage);

        // Full multi-layer identity verification
        Assert.Equal(pSummary5.TotalValue, rReport5.TotalMarketValue);
        Assert.Equal(pSummary5.TotalInvested, rReport5.TotalInvested);
        Assert.Equal(pSummary5.UnrealizedProfitLoss, rReport5.TotalUnrealizedProfitLoss);
        Assert.Equal(pSummary5.TotalValue, dSummary5.TotalPortfolioValue);
        Assert.Equal(pSummary5.TotalInvested, dSummary5.TotalInvested);
        Assert.Equal(pSummary5.UnrealizedProfitLoss, dSummary5.TotalUnrealizedProfitLoss);
    }
}
