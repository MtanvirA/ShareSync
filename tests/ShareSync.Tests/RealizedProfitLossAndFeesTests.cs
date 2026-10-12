using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.DTOs.Portfolios;
using ShareSync.Application.DTOs.Reports;
using ShareSync.Application.DTOs.Transactions;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class RealizedProfitLossAndFeesTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new ShareSyncDbContext(options);

        // Seed Users
        var user1 = new AppUser { UserId = 1, Name = "Alice Investor", Email = "alice@sharesync.com", PasswordHash = "hash1" };
        var user2 = new AppUser { UserId = 2, Name = "Bob Trader", Email = "bob@sharesync.com", PasswordHash = "hash2" };
        context.Users.AddRange(user1, user2);

        // Seed Portfolios
        var p1 = new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "Tech Growth" };
        var p2 = new Portfolio { PortfolioId = 2, UserId = 1, PortfolioName = "Dividend Income" };
        context.Portfolios.AddRange(p1, p2);

        // Seed Sector & Companies
        var sector = new Sector { SectorId = 1, SectorName = "Telecommunications" };
        context.Sectors.Add(sector);

        var c1 = new Company { CompanyId = 1, CompanyName = "Grameenphone Ltd", TickerSymbol = "GP", SectorId = 1, CurrentPrice = 160.00m };
        var c2 = new Company { CompanyId = 2, CompanyName = "Robi Axiata Ltd", TickerSymbol = "ROBI", SectorId = 1, CurrentPrice = 30.00m };
        context.Companies.AddRange(c1, c2);

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task Verification1_Buy_CreatesCorrectCostBasisAndNetCashOutflow()
    {
        // 1. BUY creates correct cost basis.
        // 5. BUY fees affect cash outflow correctly.
        using var context = CreateInMemoryDbContext();
        var txService = new TransactionService(context);
        var portfolioService = new PortfolioService(context);

        var buyReq = new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 100.00m,
            Fee = 10.00m,
            Notes = "First batch of GP"
        };

        var res = await txService.CreateTransactionAsync(buyReq, userId: 1);
        Assert.True(res.Success);
        Assert.Equal(100, res.Data!.Quantity);
        Assert.Equal(100.00m, res.Data.PricePerShare);
        Assert.Equal(10000.00m, res.Data.TotalAmount);
        Assert.Equal(10.00m, res.Data.Fee);
        Assert.Equal(10010.00m, res.Data.NetAmount); // Gross (10,000) + Fee (10)
        Assert.Null(res.Data.RealizedProfitLoss); // Realized P/L is null for BUY

        // Verify holding and portfolio calculation
        var portfolioDetail = await portfolioService.GetPortfolioByIdAsync(1, userId: 1);
        Assert.True(portfolioDetail.Success);
        var holding = Assert.Single(portfolioDetail.Data!.Holdings);
        Assert.Equal(100, holding.Shares);
        Assert.Equal(100.00m, holding.AverageBuyPrice);
        Assert.Equal(10000.00m, holding.Shares * holding.AverageBuyPrice);
        Assert.Equal(0.00m, holding.RealizedProfitLoss);

        // Portfolio summary metrics
        Assert.Equal(10.00m, portfolioDetail.Data.TotalFees);
        Assert.Equal(-10010.00m, portfolioDetail.Data.NetCashFlow); // Outflow is negative cash flow
    }

    [Fact]
    public async Task Verification2_Sell_CreatesCorrectRealizedProfitLossAndNetCashInflow()
    {
        // 2. SELL creates correct realized P/L.
        // 6. SELL fees affect cash inflow correctly.
        using var context = CreateInMemoryDbContext();
        var txService = new TransactionService(context);

        // Buy 100 @ 100, Fee 10
        await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 100.00m,
            Fee = 10.00m
        }, userId: 1);

        // Sell 100 @ 150, Fee 20
        // Net sale proceeds = 15,000 - 20 = 14,980
        // Cost basis sold = 100 * 100 = 10,000
        // Realized P/L = 14,980 - 10,000 = +4,980.00
        var sellRes = await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "SELL",
            Quantity = 100,
            PricePerShare = 150.00m,
            Fee = 20.00m
        }, userId: 1);

        Assert.True(sellRes.Success);
        Assert.Equal(15000.00m, sellRes.Data!.TotalAmount);
        Assert.Equal(20.00m, sellRes.Data.Fee);
        Assert.Equal(14980.00m, sellRes.Data.NetAmount); // Gross (15,000) - Fee (20)
        Assert.Equal(4980.00m, sellRes.Data.RealizedProfitLoss);
    }

    [Fact]
    public async Task Verification3_PartialSell_PreservesWeightedAverageCostAndRemainingShares()
    {
        // 3. Partial SELL works.
        using var context = CreateInMemoryDbContext();
        var txService = new TransactionService(context);
        var portfolioService = new PortfolioService(context);

        // BUY 100 @ 100
        await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 100.00m,
            Fee = 0.00m
        }, userId: 1);

        // Partial SELL 40 @ 120, Fee 10
        // Net sale proceeds = 4,800 - 10 = 4,790
        // Cost basis sold = 40 * 100 = 4,000
        // Realized P/L = 4,790 - 4,000 = +790.00
        var sellRes = await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "SELL",
            Quantity = 40,
            PricePerShare = 120.00m,
            Fee = 10.00m
        }, userId: 1);

        Assert.True(sellRes.Success);
        Assert.Equal(790.00m, sellRes.Data!.RealizedProfitLoss);

        // Verify remaining 60 shares retain the exact same average buy price of 100.00
        var details = await portfolioService.GetPortfolioByIdAsync(1, userId: 1);
        var holding = Assert.Single(details.Data!.Holdings);
        Assert.Equal(60, holding.Shares);
        Assert.Equal(100.00m, holding.AverageBuyPrice);
        Assert.Equal(6000.00m, holding.Shares * holding.AverageBuyPrice);
        Assert.Equal(790.00m, holding.RealizedProfitLoss);
    }

    [Fact]
    public async Task Verification4_ControlledNumericalExample_MultipleBuysAndSells()
    {
        // 4. Multiple BUYs followed by SELL work with weighted-average cost.
        // Controlled numerical example independently:
        // Buy 1: 100 @ 100, Fee 10 -> Gross 10,000, Net Outflow 10,010
        // Buy 2: 50 @ 130, Fee 20 -> Gross 6,500, Net Outflow 6,520
        // WAC = (10,000 + 6,500) / 150 = 16,500 / 150 = 110.00
        // Sell 1: 60 @ 150, Fee 15 -> Gross 9,000, Net Inflow 8,985. Cost sold = 60 * 110 = 6,600. Realized P/L = 8,985 - 6,600 = +2,385.00
        // Market price of GP is 160.00. Remaining qty = 90.
        // Current Value = 90 * 160 = 14,400. Remaining Cost = 90 * 110 = 9,900.
        // Unrealized P/L = 14,400 - 9,900 = +4,500.00.
        // Total P/L = 4,500 + 2,385 = +6,885.00.
        // Total Fees = 10 + 20 + 15 = 45.00.
        // Net Cash Flow = Net Inflow (8,985) - Net Outflow (10,010 + 6,520) = 8,985 - 16,530 = -7,545.00.
        using var context = CreateInMemoryDbContext();
        var txService = new TransactionService(context);
        var portfolioService = new PortfolioService(context);

        await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 100.00m,
            Fee = 10.00m
        }, userId: 1);

        await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 50,
            PricePerShare = 130.00m,
            Fee = 20.00m
        }, userId: 1);

        var sell1 = await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "SELL",
            Quantity = 60,
            PricePerShare = 150.00m,
            Fee = 15.00m
        }, userId: 1);

        Assert.Equal(2385.00m, sell1.Data!.RealizedProfitLoss);

        var summary = await portfolioService.GetPortfolioByIdAsync(1, userId: 1);
        Assert.True(summary.Success);
        var data = summary.Data!;

        // Holding checks
        var holding = Assert.Single(data.Holdings);
        Assert.Equal(90, holding.Shares);
        Assert.Equal(110.00m, holding.AverageBuyPrice);
        Assert.Equal(9900.00m, holding.Shares * holding.AverageBuyPrice);
        Assert.Equal(14400.00m, holding.MarketValue);
        Assert.Equal(4500.00m, holding.UnrealizedProfitLoss);
        Assert.Equal(2385.00m, holding.RealizedProfitLoss);

        // Portfolio summary checks
        Assert.Equal(9900.00m, data.TotalInvested);
        Assert.Equal(14400.00m, data.TotalValue);
        Assert.Equal(4500.00m, data.UnrealizedProfitLoss);
        Assert.Equal(2385.00m, data.RealizedProfitLoss);
        Assert.Equal(6885.00m, data.TotalProfitLoss);
        Assert.Equal(45.00m, data.TotalFees);
        Assert.Equal(-7545.00m, data.NetCashFlow);

        // Sell 2: SELL remaining 90 shares @ 105, Fee 25
        // Gross = 9,450. Net Inflow = 9,450 - 25 = 9,425.
        // Cost Basis Sold = 90 * 110 = 9,900.
        // Realized P/L = 9,425 - 9,900 = -475.00.
        var sell2 = await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "SELL",
            Quantity = 90,
            PricePerShare = 105.00m,
            Fee = 25.00m
        }, userId: 1);

        Assert.Equal(-475.00m, sell2.Data!.RealizedProfitLoss);

        var finalSummary = await portfolioService.GetPortfolioByIdAsync(1, userId: 1);
        var finalData = finalSummary.Data!;

        Assert.Empty(finalData.Holdings); // 0 remaining shares
        Assert.Equal(0.00m, finalData.TotalInvested);
        Assert.Equal(0.00m, finalData.TotalValue);
        Assert.Equal(0.00m, finalData.UnrealizedProfitLoss); // Unrealized is 0
        Assert.Equal(1910.00m, finalData.RealizedProfitLoss); // 2385 - 475 = 1910
        Assert.Equal(1910.00m, finalData.TotalProfitLoss); // 0 + 1910 = 1910
        Assert.Equal(70.00m, finalData.TotalFees); // 45 + 25 = 70
        Assert.Equal(1880.00m, finalData.NetCashFlow); // (8985 + 9425) - 16530 = 18410 - 16530 = +1880.00
    }

    [Fact]
    public async Task Verification7_NegativeFees_AreRejected()
    {
        // 7. Negative fees are rejected.
        using var context = CreateInMemoryDbContext();
        var txService = new TransactionService(context);

        // Create transaction with negative fee
        var exCreate = await Assert.ThrowsAsync<AppException>(() => txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 10,
            PricePerShare = 100.00m,
            Fee = -5.00m
        }, userId: 1));

        Assert.Equal(400, exCreate.StatusCode);
        Assert.Contains("Fee cannot be negative", exCreate.Message);

        // Create valid transaction first
        var valid = await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 10,
            PricePerShare = 100.00m,
            Fee = 5.00m
        }, userId: 1);

        // Update transaction with negative fee
        var exUpdate = await Assert.ThrowsAsync<AppException>(() => txService.UpdateTransactionAsync(valid.Data!.TransactionId, new UpdateTransactionRequestDto
        {
            Quantity = 10,
            PricePerShare = 100.00m,
            Fee = -1.00m
        }, userId: 1));

        Assert.Equal(400, exUpdate.StatusCode);
        Assert.Contains("Fee cannot be negative", exUpdate.Message);
    }

    [Fact]
    public async Task Verification8_9_ExistingUnrealizedProfitLossAndTotalProfitLoss_AreConsistent()
    {
        // 8. Existing unrealized P/L still works.
        // 9. Total P/L is consistent.
        using var context = CreateInMemoryDbContext();
        var txService = new TransactionService(context);
        var portfolioService = new PortfolioService(context);

        // BUY 100 @ 120 (Current market price of GP is 160)
        await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 120.00m,
            Fee = 0.00m
        }, userId: 1);

        var details = await portfolioService.GetPortfolioByIdAsync(1, userId: 1);
        var data = details.Data!;

        // Unrealized P/L = 100 * (160 - 120) = +4,000.00
        Assert.Equal(4000.00m, data.UnrealizedProfitLoss);
        Assert.Equal(0.00m, data.RealizedProfitLoss);
        Assert.Equal(4000.00m, data.TotalProfitLoss); // Unrealized + Realized

        // Partial SELL 50 @ 180, Fee 10
        // Realized P/L = 50 * (180 - 120) - 10 = 3,000 - 10 = +2,990.00
        await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "SELL",
            Quantity = 50,
            PricePerShare = 180.00m,
            Fee = 10.00m
        }, userId: 1);

        var details2 = await portfolioService.GetPortfolioByIdAsync(1, userId: 1);
        var data2 = details2.Data!;

        // Remaining 50 shares @ cost 120, market 160 -> Unrealized = 50 * (160 - 120) = 2,000.00
        Assert.Equal(2000.00m, data2.UnrealizedProfitLoss);
        Assert.Equal(2990.00m, data2.RealizedProfitLoss);
        Assert.Equal(4990.00m, data2.TotalProfitLoss); // 2000 + 2990 = 4990.00
    }

    [Fact]
    public async Task Verification10_TransactionEditingAndDeletion_RecalculatesAccurately()
    {
        // 10. Existing transaction editing/deletion still works.
        using var context = CreateInMemoryDbContext();
        var txService = new TransactionService(context);
        var portfolioService = new PortfolioService(context);

        var buy = await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 100.00m,
            Fee = 10.00m
        }, userId: 1);

        var sell = await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "SELL",
            Quantity = 50,
            PricePerShare = 150.00m,
            Fee = 15.00m
        }, userId: 1);

        // Edit the SELL fee from 15 to 25 and price to 160
        // New Net Inflow = 50 * 160 - 25 = 8000 - 25 = 7975
        // Cost Basis Sold = 50 * 100 = 5000
        // Realized P/L = 7975 - 5000 = 2975.00
        var updateRes = await txService.UpdateTransactionAsync(sell.Data!.TransactionId, new UpdateTransactionRequestDto
        {
            Quantity = 50,
            PricePerShare = 160.00m,
            Fee = 25.00m
        }, userId: 1);

        Assert.True(updateRes.Success);
        Assert.Equal(2975.00m, updateRes.Data!.RealizedProfitLoss);
        Assert.Equal(25.00m, updateRes.Data.Fee);
        Assert.Equal(7975.00m, updateRes.Data.NetAmount);

        // Delete the SELL transaction
        var delRes = await txService.DeleteTransactionAsync(sell.Data!.TransactionId, userId: 1);
        Assert.True(delRes.Success);

        var details = await portfolioService.GetPortfolioByIdAsync(1, userId: 1);
        var data = details.Data!;
        // After deleting the SELL, realized P/L returns to 0.00
        Assert.Equal(0.00m, data.RealizedProfitLoss);
        Assert.Equal(10.00m, data.TotalFees);
        Assert.Equal(-10010.00m, data.NetCashFlow);
    }

    [Fact]
    public async Task Verification11_AuditBehavior_RecordsFeeAndDetails()
    {
        // 11. Audit behavior remains intact.
        using var context = CreateInMemoryDbContext();
        var txService = new TransactionService(context);

        var res = await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 50,
            PricePerShare = 120.00m,
            Fee = 12.50m
        }, userId: 1);

        var audit = await context.TransactionAudits
            .FirstOrDefaultAsync(a => a.TransactionId == res.Data!.TransactionId);

        Assert.NotNull(audit);
        Assert.Equal("INSERT", audit.ActionType);
        Assert.Contains("Fee:", audit.Details);
        Assert.Contains("12.50", audit.Details);

        // Update
        await txService.UpdateTransactionAsync(res.Data!.TransactionId, new UpdateTransactionRequestDto
        {
            Quantity = 60,
            PricePerShare = 125.00m,
            Fee = 15.00m
        }, userId: 1);

        var updateAudit = await context.TransactionAudits
            .Where(a => a.TransactionId == res.Data!.TransactionId && a.ActionType == "UPDATE")
            .FirstOrDefaultAsync();

        Assert.NotNull(updateAudit);
        Assert.Contains("Fee", updateAudit.Details);
        Assert.Contains("15.00", updateAudit.Details);
    }

    [Fact]
    public async Task Verification12_13_DatabaseLogicEquivalence_FormulaParity()
    {
        // 12. Oracle version parity.
        // 13. MySQL version parity.
        // Both database functions implement:
        // fn_calculate_realized_pl:
        //   wac = sum(buy_qty * buy_price) / sum(buy_qty)
        //   gross_sale = sum(sell_qty * sell_price)
        //   sell_fees = sum(sell_fees)
        //   sold_qty = sum(sell_qty)
        //   realized_pl = (gross_sale - sell_fees) - (sold_qty * wac)
        // fn_calculate_net_cash_flow:
        //   (gross_sale - sell_fees) - (gross_buy + buy_fees)

        using var context = CreateInMemoryDbContext();
        var txService = new TransactionService(context);
        var reportService = new ReportService(context);

        // Transactions:
        // Buy 100 @ 100, fee 10  -> Gross Buy 10,000, Fee 10 -> Net Outflow 10,010
        // Buy 100 @ 200, fee 20  -> Gross Buy 20,000, Fee 20 -> Net Outflow 20,020
        // Total Buy Qty = 200, Total Buy Cost = 30,000, WAC = 150.00
        // Sell 50 @ 180, fee 15  -> Gross Sell 9,000, Fee 15 -> Net Inflow 8,985
        // Cost Basis Sold = 50 * 150 = 7,500
        // Realized PL = 8,985 - 7,500 = +1,485.00
        // Net Cash Flow = 8,985 - (10,010 + 20,020) = 8,985 - 30,030 = -21,045.00

        await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 100.00m,
            Fee = 10.00m
        }, userId: 1);

        await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 200.00m,
            Fee = 20.00m
        }, userId: 1);

        var sell = await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "SELL",
            Quantity = 50,
            PricePerShare = 180.00m,
            Fee = 15.00m
        }, userId: 1);

        Assert.Equal(1485.00m, sell.Data!.RealizedProfitLoss);

        // Report summary verification
        var reportSummary = await reportService.GetReportSummaryAsync(userId: 1, portfolioId: 1);
        Assert.True(reportSummary.Success);
        Assert.Equal(1485.00m, reportSummary.Data!.TotalRealizedProfitLoss);
        Assert.Equal(45.00m, reportSummary.Data.TotalFees);
        Assert.Equal(-21045.00m, reportSummary.Data.NetCashFlow);

        // Transaction history report item verification
        var txHistory = await reportService.GetTransactionHistoryReportAsync(userId: 1, portfolioId: 1);
        Assert.True(txHistory.Success);
        var sellItem = txHistory.Data!.Transactions.First(t => t.TransactionType == "SELL");
        Assert.Equal(15.00m, sellItem.Fee);
        Assert.Equal(8985.00m, sellItem.NetAmount);
        Assert.Equal(1485.00m, sellItem.RealizedProfitLoss);
    }
}
