using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.DTOs.Transactions;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class AdvancedTransactionFilterTests
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
        var p1 = new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "Tech Portfolio" };
        var p2 = new Portfolio { PortfolioId = 2, UserId = 1, PortfolioName = "Growth Portfolio" };
        var pOther = new Portfolio { PortfolioId = 3, UserId = 2, PortfolioName = "User 2 Portfolio" };
        context.Portfolios.AddRange(p1, p2, pOther);

        // Seed companies
        var sector = new Sector { SectorId = 1, SectorName = "Technology" };
        context.Sectors.Add(sector);

        var c1 = new Company { CompanyId = 1, CompanyName = "Grameenphone", TickerSymbol = "GP", SectorId = 1, CurrentPrice = 300m };
        var c2 = new Company { CompanyId = 2, CompanyName = "Beximco Pharma", TickerSymbol = "BEXIMCO", SectorId = 1, CurrentPrice = 120m };
        context.Companies.AddRange(c1, c2);

        // Seed diverse transactions for User 1
        var t1 = new Transaction
        {
            TransactionId = 101,
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 300.00m,
            TransactionDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            Portfolio = p1,
            Company = c1
        };

        var t2 = new Transaction
        {
            TransactionId = 102,
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 50,
            PricePerShare = 320.00m,
            TransactionDate = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            Portfolio = p1,
            Company = c1
        };

        var t3 = new Transaction
        {
            TransactionId = 103,
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "SELL",
            Quantity = 30,
            PricePerShare = 350.00m,
            TransactionDate = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc),
            Portfolio = p1,
            Company = c1
        };

        var t4 = new Transaction
        {
            TransactionId = 104,
            PortfolioId = 2,
            CompanyId = 2,
            TransactionType = "BUY",
            Quantity = 200,
            PricePerShare = 110.00m,
            TransactionDate = new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),
            Portfolio = p2,
            Company = c2
        };

        var t5 = new Transaction
        {
            TransactionId = 105,
            PortfolioId = 2,
            CompanyId = 2,
            TransactionType = "SELL",
            Quantity = 50,
            PricePerShare = 130.00m,
            TransactionDate = new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Utc),
            Portfolio = p2,
            Company = c2
        };

        // Seed transaction for User 2 (Portfolio 3)
        var tOther = new Transaction
        {
            TransactionId = 106,
            PortfolioId = 3,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 500,
            PricePerShare = 290.00m,
            TransactionDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Portfolio = pOther,
            Company = c1
        };

        context.Transactions.AddRange(t1, t2, t3, t4, t5, tOther);
        context.SaveChanges();

        return context;
    }

    [Fact]
    public async Task FilterByPortfolio_ReturnsOnlySelectedPortfolioTransactions()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var filter = new TransactionFilterDto { PortfolioId = 1 };
        var res = await service.GetPagedTransactionsAsync(1, filter);

        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        Assert.Equal(3, res.Data.TotalItems);
        Assert.All(res.Data.Items, t => Assert.Equal(1, t.PortfolioId));
    }

    [Fact]
    public async Task FilterByCompany_ReturnsOnlyMatchingCompany()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var filter = new TransactionFilterDto { CompanyId = 2 };
        var res = await service.GetPagedTransactionsAsync(1, filter);

        Assert.True(res.Success);
        Assert.Equal(2, res.Data!.TotalItems);
        Assert.All(res.Data.Items, t => Assert.Equal(2, t.CompanyId));
    }

    [Fact]
    public async Task FilterByType_BUY_ReturnsOnlyBuys()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var filter = new TransactionFilterDto { TransactionType = "BUY" };
        var res = await service.GetPagedTransactionsAsync(1, filter);

        Assert.True(res.Success);
        Assert.Equal(3, res.Data!.TotalItems);
        Assert.All(res.Data.Items, t => Assert.Equal("BUY", t.TransactionType));
    }

    [Fact]
    public async Task FilterByType_SELL_ReturnsOnlySells()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var filter = new TransactionFilterDto { TransactionType = "SELL" };
        var res = await service.GetPagedTransactionsAsync(1, filter);

        Assert.True(res.Success);
        Assert.Equal(2, res.Data!.TotalItems);
        Assert.All(res.Data.Items, t => Assert.Equal("SELL", t.TransactionType));
    }

    [Fact]
    public async Task FilterByDateRange_FiltersCorrectly()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var filter = new TransactionFilterDto
        {
            StartDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc)
        };
        var res = await service.GetPagedTransactionsAsync(1, filter);

        Assert.True(res.Success);
        // t2 (Feb 15), t3 (Mar 20), t4 (Apr 5)
        Assert.Equal(3, res.Data!.TotalItems);
    }

    [Fact]
    public async Task FilterByPriceRange_MinAndMax_Works()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var filter = new TransactionFilterDto
        {
            MinPrice = 120.00m,
            MaxPrice = 310.00m
        };
        var res = await service.GetPagedTransactionsAsync(1, filter);

        Assert.True(res.Success);
        // t1 (300), t5 (130)
        Assert.Equal(2, res.Data!.TotalItems);
        Assert.All(res.Data.Items, t =>
        {
            Assert.True(t.PricePerShare >= 120.00m && t.PricePerShare <= 310.00m);
        });
    }

    [Fact]
    public async Task FilterByQuantityRange_Works()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var filter = new TransactionFilterDto
        {
            MinQuantity = 100,
            MaxQuantity = 250
        };
        var res = await service.GetPagedTransactionsAsync(1, filter);

        Assert.True(res.Success);
        // t1 (100), t4 (200)
        Assert.Equal(2, res.Data!.TotalItems);
    }

    [Fact]
    public async Task CombinedFilters_WorksAccurately()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var filter = new TransactionFilterDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            MinPrice = 310.00m
        };
        var res = await service.GetPagedTransactionsAsync(1, filter);

        Assert.True(res.Success);
        // t2 only: Portfolio 1, Company 1, BUY, Price 320
        Assert.Equal(1, res.Data!.TotalItems);
        Assert.Equal(102, res.Data.Items.First().TransactionId);
    }

    [Theory]
    [InlineData("date", "asc", 101)]
    [InlineData("date", "desc", 105)]
    [InlineData("quantity", "asc", 103)] // 30 shares
    [InlineData("quantity", "desc", 104)] // 200 shares
    [InlineData("price", "asc", 104)] // 110.00
    [InlineData("price", "desc", 103)] // 350.00
    [InlineData("value", "asc", 105)] // t5: 50 * 130 = 6500
    [InlineData("value", "desc", 101)] // t1: 100 * 300 = 30000
    public async Task Sorting_VariousColumns_OrdersExpectedly(string sortBy, string sortDir, int expectedFirstId)
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var filter = new TransactionFilterDto
        {
            SortBy = sortBy,
            SortDirection = sortDir
        };

        var res = await service.GetPagedTransactionsAsync(1, filter);

        Assert.True(res.Success);
        Assert.Equal(5, res.Data!.TotalItems);
        Assert.Equal(expectedFirstId, res.Data.Items.First().TransactionId);
    }

    [Fact]
    public async Task Pagination_ReturnsCorrectPageAndPageSize()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        // Page 1 with pageSize 2 (descending by date by default)
        var p1Filter = new TransactionFilterDto { Page = 1, PageSize = 2, SortBy = "date", SortDirection = "desc" };
        var p1Res = await service.GetPagedTransactionsAsync(1, p1Filter);

        Assert.True(p1Res.Success);
        Assert.Equal(1, p1Res.Data!.Page);
        Assert.Equal(2, p1Res.Data.PageSize);
        Assert.Equal(5, p1Res.Data.TotalItems);
        Assert.Equal(3, p1Res.Data.TotalPages);
        Assert.Equal(2, p1Res.Data.Items.Count);
        Assert.Equal(105, p1Res.Data.Items[0].TransactionId); // May 12
        Assert.Equal(104, p1Res.Data.Items[1].TransactionId); // Apr 5

        // Page 2
        var p2Filter = new TransactionFilterDto { Page = 2, PageSize = 2, SortBy = "date", SortDirection = "desc" };
        var p2Res = await service.GetPagedTransactionsAsync(1, p2Filter);

        Assert.Equal(2, p2Res.Data!.Page);
        Assert.Equal(2, p2Res.Data.Items.Count);
        Assert.Equal(103, p2Res.Data.Items[0].TransactionId); // Mar 20
        Assert.Equal(102, p2Res.Data.Items[1].TransactionId); // Feb 15

        // Page 3
        var p3Filter = new TransactionFilterDto { Page = 3, PageSize = 2, SortBy = "date", SortDirection = "desc" };
        var p3Res = await service.GetPagedTransactionsAsync(1, p3Filter);

        Assert.Equal(3, p3Res.Data!.Page);
        Assert.Single(p3Res.Data.Items);
        Assert.Equal(101, p3Res.Data.Items[0].TransactionId); // Jan 10
    }

    [Fact]
    public async Task EmptyResults_ReturnsZeroTotalItemsAndEmptyList()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var filter = new TransactionFilterDto
        {
            MinPrice = 9999.00m
        };

        var res = await service.GetPagedTransactionsAsync(1, filter);

        Assert.True(res.Success);
        Assert.Equal(0, res.Data!.TotalItems);
        Assert.Empty(res.Data.Items);
        Assert.Equal(0, res.Data.TotalPages);
    }

    [Fact]
    public async Task InvalidDateRange_ThrowsBadRequestAppException()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var filter = new TransactionFilterDto
        {
            StartDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var ex = await Assert.ThrowsAsync<AppException>(() => service.GetPagedTransactionsAsync(1, filter));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("Start date cannot be after end date", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UserIsolation_CannotQueryOtherUsersPortfolio()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        // Portfolio 3 belongs to User 2
        var filter = new TransactionFilterDto { PortfolioId = 3 };

        var ex = await Assert.ThrowsAsync<AppException>(() => service.GetPagedTransactionsAsync(1, filter));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task UserIsolation_UnfilteredQueryNeverExposesOtherUserData()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TransactionService(context);

        var filter = new TransactionFilterDto(); // Unfiltered query for User 1
        var res = await service.GetPagedTransactionsAsync(1, filter);

        Assert.True(res.Success);
        Assert.Equal(5, res.Data!.TotalItems);
        // Transaction 106 belongs to User 2 (Portfolio 3) and must NOT be present
        Assert.DoesNotContain(res.Data.Items, t => t.TransactionId == 106);
    }
}
