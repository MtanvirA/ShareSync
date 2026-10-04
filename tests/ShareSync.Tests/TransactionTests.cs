using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.DTOs.Transactions;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ShareSync.Tests;

public class TransactionTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new ShareSyncDbContext(options);

        // Seed reference data
        var user1 = new AppUser { UserId = 1, Name = "User One", Email = "user1@sharesync.com", PasswordHash = "hash" };
        var user2 = new AppUser { UserId = 2, Name = "User Two", Email = "user2@sharesync.com", PasswordHash = "hash" };
        context.Users.AddRange(user1, user2);

        var p1 = new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "Portfolio 1" };
        var p2 = new Portfolio { PortfolioId = 2, UserId = 1, PortfolioName = "Portfolio 2" };
        var pOther = new Portfolio { PortfolioId = 3, UserId = 2, PortfolioName = "User 2 Portfolio" };
        context.Portfolios.AddRange(p1, p2, pOther);

        var sector = new Sector { SectorId = 1, SectorName = "Technology" };
        context.Sectors.Add(sector);

        var c1 = new Company { CompanyId = 1, CompanyName = "Grameenphone", TickerSymbol = "GP", SectorId = 1, CurrentPrice = 300m };
        var c2 = new Company { CompanyId = 2, CompanyName = "Beximco Pharma", TickerSymbol = "BEXIMCO", SectorId = 1, CurrentPrice = 120m };
        context.Companies.AddRange(c1, c2);

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task CreateTransaction_Buy_SucceedsAndAudits()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var request = new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 280.00m,
            Notes = "Initial BUY"
        };

        var response = await service.CreateTransactionAsync(request, userId: 1);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(100, response.Data.Quantity);
        Assert.Equal(280.00m, response.Data.PricePerShare);
        Assert.Equal(28000.00m, response.Data.TotalAmount);
        Assert.Equal("BUY", response.Data.TransactionType);

        // Verify audit trail
        var audit = await context.TransactionAudits.FirstOrDefaultAsync(a => a.TransactionId == response.Data.TransactionId);
        Assert.NotNull(audit);
        Assert.Equal("INSERT", audit.ActionType);
        Assert.Contains("BUY", audit.Details);
    }

    [Fact]
    public async Task CreateTransaction_MultipleBuys_IncreasesAvailableHoldings()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        // BUY 100
        await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 280.00m
        }, userId: 1);

        // BUY 50
        await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 50,
            PricePerShare = 290.00m
        }, userId: 1);

        var available = await service.GetAvailableQuantityAsync(portfolioId: 1, companyId: 1, userId: 1);
        Assert.Equal(150, available.Data);
    }

    [Fact]
    public async Task CreateTransaction_PartialSell_DecreasesHoldingsSuccessfully()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        // BUY 100
        await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 250.00m
        }, userId: 1);

        // SELL 40
        var sellRes = await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "SELL",
            Quantity = 40,
            PricePerShare = 300.00m
        }, userId: 1);

        Assert.True(sellRes.Success);
        var available = await service.GetAvailableQuantityAsync(portfolioId: 1, companyId: 1, userId: 1);
        Assert.Equal(60, available.Data);
    }

    [Fact]
    public async Task CreateTransaction_CompleteSell_LeavesZeroHoldings()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        // BUY 75
        await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 75,
            PricePerShare = 200.00m
        }, userId: 1);

        // SELL 75 (complete liquidation)
        var sellRes = await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "SELL",
            Quantity = 75,
            PricePerShare = 220.00m
        }, userId: 1);

        Assert.True(sellRes.Success);
        var available = await service.GetAvailableQuantityAsync(portfolioId: 1, companyId: 1, userId: 1);
        Assert.Equal(0, available.Data);
    }

    [Fact]
    public async Task CreateTransaction_AttemptedOversell_ThrowsBusinessRuleExceptionAndDoesNotPersist()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        // BUY 50
        await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 50,
            PricePerShare = 100.00m
        }, userId: 1);

        // Attempt to SELL 51 (Oversell!)
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "SELL",
            Quantity = 51,
            PricePerShare = 120.00m
        }, userId: 1));

        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("exceeds available holdings", ex.Message);

        // Holdings must remain unchanged at 50
        var available = await service.GetAvailableQuantityAsync(portfolioId: 1, companyId: 1, userId: 1);
        Assert.Equal(50, available.Data);
    }

    [Fact]
    public async Task CreateTransaction_SellWithoutAnyBuys_ThrowsBusinessRuleException()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        // Attempt to SELL without owning any shares
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 2,
            TransactionType = "SELL",
            Quantity = 10,
            PricePerShare = 120.00m
        }, userId: 1));

        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("exceeds available holdings", ex.Message);
    }

    [Fact]
    public async Task CreateTransaction_MultipleCompanies_IsolatesHoldingsPerCompany()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        // BUY 100 of Company 1 (GP)
        await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 300.00m
        }, userId: 1);

        // BUY 200 of Company 2 (BEXIMCO)
        await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 2,
            TransactionType = "BUY",
            Quantity = 200,
            PricePerShare = 120.00m
        }, userId: 1);

        var gpAvail = await service.GetAvailableQuantityAsync(portfolioId: 1, companyId: 1, userId: 1);
        var bxAvail = await service.GetAvailableQuantityAsync(portfolioId: 1, companyId: 2, userId: 1);

        Assert.Equal(100, gpAvail.Data);
        Assert.Equal(200, bxAvail.Data);

        // SELL 150 of BEXIMCO should succeed without affecting GP
        var sellBx = await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 2,
            TransactionType = "SELL",
            Quantity = 150,
            PricePerShare = 130.00m
        }, userId: 1);

        Assert.True(sellBx.Success);

        gpAvail = await service.GetAvailableQuantityAsync(portfolioId: 1, companyId: 1, userId: 1);
        bxAvail = await service.GetAvailableQuantityAsync(portfolioId: 1, companyId: 2, userId: 1);

        Assert.Equal(100, gpAvail.Data);
        Assert.Equal(50, bxAvail.Data);
    }

    [Fact]
    public async Task CreateTransaction_MultiplePortfolios_IsolatesHoldingsPerPortfolio()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        // User 1 buys in Portfolio 1
        await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 50,
            PricePerShare = 200.00m
        }, userId: 1);

        // In Portfolio 2, User 1 does not have any shares yet
        var p2Avail = await service.GetAvailableQuantityAsync(portfolioId: 2, companyId: 1, userId: 1);
        Assert.Equal(0, p2Avail.Data);

        // Attempting to sell from Portfolio 2 must fail even if Portfolio 1 has shares
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 2,
            CompanyId = 1,
            TransactionType = "SELL",
            Quantity = 20,
            PricePerShare = 220.00m
        }, userId: 1));

        Assert.Equal(422, ex.StatusCode);
    }

    [Fact]
    public async Task CreateTransaction_UnauthorizedPortfolio_ThrowsForbidden()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        // User 1 attempts to create a transaction in Portfolio 3 (owned by User 2)
        var ex = await Assert.ThrowsAsync<AppException>(() => service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 3,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 10,
            PricePerShare = 100.00m
        }, userId: 1));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task CreateTransaction_InvalidPortfolio_ThrowsNotFound()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 9999,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 10,
            PricePerShare = 100.00m
        }, userId: 1));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task CreateTransaction_InvalidCompany_ThrowsNotFound()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 8888,
            TransactionType = "BUY",
            Quantity = 10,
            PricePerShare = 100.00m
        }, userId: 1));

        Assert.Equal(404, ex.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task CreateTransaction_ZeroOrNegativeQuantity_ThrowsBadRequest(decimal invalidQty)
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = invalidQty,
            PricePerShare = 100.00m
        }, userId: 1));

        Assert.Equal(400, ex.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task CreateTransaction_ZeroOrNegativePrice_ThrowsBadRequest(decimal invalidPrice)
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 10,
            PricePerShare = invalidPrice
        }, userId: 1));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CreateTransaction_InvalidType_ThrowsBadRequest()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "HOLD",
            Quantity = 10,
            PricePerShare = 100.00m
        }, userId: 1));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CreateTransaction_FutureDate_ThrowsBadRequest()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 10,
            PricePerShare = 100.00m,
            TransactionDate = DateTime.UtcNow.AddDays(5)
        }, userId: 1));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("future", ex.Message);
    }

    [Fact]
    public async Task UpdateTransaction_WhenValid_UpdatesAndAudits()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var created = await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 150.00m
        }, userId: 1);

        var txId = created.Data!.TransactionId;

        // Update quantity to 120 and price to 155
        var updateRes = await service.UpdateTransactionAsync(txId, new UpdateTransactionRequestDto
        {
            Quantity = 120,
            PricePerShare = 155.00m,
            Notes = "Adjusted order"
        }, userId: 1);

        Assert.True(updateRes.Success);
        Assert.Equal(120, updateRes.Data!.Quantity);
        Assert.Equal(155.00m, updateRes.Data.PricePerShare);

        // Verify update audit
        var updateAudit = await context.TransactionAudits.FirstOrDefaultAsync(a => a.TransactionId == txId && a.ActionType == "UPDATE");
        Assert.NotNull(updateAudit);
        Assert.Contains("Adjusted order", updateAudit.Details);
    }

    [Fact]
    public async Task UpdateTransaction_WhenReducingBuyCausesNegativeHoldings_ThrowsBusinessRuleException()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var buy = await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 150.00m
        }, userId: 1);

        // SELL 80
        await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "SELL",
            Quantity = 80,
            PricePerShare = 160.00m
        }, userId: 1);

        // Attempt to update the BUY quantity from 100 down to 70 (would make holdings 70 - 80 = -10!)
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateTransactionAsync(buy.Data!.TransactionId, new UpdateTransactionRequestDto
        {
            Quantity = 70,
            PricePerShare = 150.00m
        }, userId: 1));

        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("negative holdings", ex.Message);
    }

    [Fact]
    public async Task DeleteTransaction_WhenBuyDependedBySell_ThrowsBusinessRuleException()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var buy = await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 50,
            PricePerShare = 100.00m
        }, userId: 1);

        // SELL 30
        await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "SELL",
            Quantity = 30,
            PricePerShare = 120.00m
        }, userId: 1);

        // Deleting the BUY of 50 would leave 0 - 30 = -30
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteTransactionAsync(buy.Data!.TransactionId, userId: 1));
        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("Existing SELL orders require these shares", ex.Message);
    }

    [Fact]
    public async Task DeleteTransaction_WhenSafe_DeletesAndRetainsAudit()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var buy = await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 50,
            PricePerShare = 100.00m
        }, userId: 1);

        var txId = buy.Data!.TransactionId;

        // Delete the buy (no sells exist, safe)
        var delRes = await service.DeleteTransactionAsync(txId, userId: 1);
        Assert.True(delRes.Success);

        // Verify transaction is deleted
        var found = await context.Transactions.FindAsync(txId);
        Assert.Null(found);

        // Verify DELETE audit entry was created and preserved
        var deleteAudit = await context.TransactionAudits.FirstOrDefaultAsync(a => a.ActionType == "DELETE");
        Assert.NotNull(deleteAudit);
        Assert.Contains($"Deleted transaction #{txId}", deleteAudit.Details);
    }

    [Fact]
    public async Task CreateTransaction_SqlInjectionInNotes_SafelyEscapesAndAudits()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);
        const string sqli = "'; DROP TABLE TRANSACTIONS; --";

        var res = await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 10,
            PricePerShare = 100.00m,
            Notes = sqli
        }, userId: 1);

        Assert.True(res.Success);
        var audit = await context.TransactionAudits.FirstOrDefaultAsync(a => a.TransactionId == res.Data!.TransactionId);
        Assert.NotNull(audit);
        Assert.Contains(sqli, audit.Details);

        // Table still intact
        Assert.True(await context.Transactions.AnyAsync());
    }

    [Fact]
    public async Task CreateTransaction_WhenSellExceedsHoldings_RollsBackAndLeavesZeroTransactions()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        // Verify initial count is 0
        Assert.Empty(await context.Transactions.ToListAsync());

        // Attempt invalid sell
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "SELL",
            Quantity = 10,
            PricePerShare = 100.00m
        }, userId: 1));

        // Verify rollback / no partial transaction exists
        Assert.Empty(await context.Transactions.ToListAsync());
        Assert.Empty(await context.TransactionAudits.ToListAsync());
    }

    [Fact]
    public async Task CreateTransaction_BuyFollowedBySell_MaintainsAccurateHistoryAndOrder()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var buy1 = await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 200.00m
        }, userId: 1);

        var sell1 = await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "SELL",
            Quantity = 40,
            PricePerShare = 250.00m
        }, userId: 1);

        var buy2 = await service.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 50,
            PricePerShare = 240.00m
        }, userId: 1);

        // Current holdings: 100 - 40 + 50 = 110
        var available = await service.GetAvailableQuantityAsync(portfolioId: 1, companyId: 1, userId: 1);
        Assert.Equal(110, available.Data);

        var history = await service.GetUserTransactionsAsync(userId: 1, new TransactionFilterDto { PortfolioId = 1 });
        Assert.Equal(3, history.Data!.Count);
    }
}

