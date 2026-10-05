using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class GlobalSearchTests
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

        // Seed sectors
        var s1 = new Sector { SectorId = 1, SectorName = "Telecommunications", Description = "Telecom providers" };
        var s2 = new Sector { SectorId = 2, SectorName = "Pharmaceuticals", Description = "Pharma and healthcare" };
        context.Sectors.AddRange(s1, s2);

        // Seed companies
        var c1 = new Company { CompanyId = 1, CompanyName = "Grameenphone Ltd.", TickerSymbol = "GP", SectorId = 1, CurrentPrice = 310m };
        var c2 = new Company { CompanyId = 2, CompanyName = "Beximco Pharmaceuticals", TickerSymbol = "BEXIMCO", SectorId = 2, CurrentPrice = 125m };
        var c3 = new Company { CompanyId = 3, CompanyName = "Square Pharmaceuticals", TickerSymbol = "SQURPHARMA", SectorId = 2, CurrentPrice = 240m };
        context.Companies.AddRange(c1, c2, c3);

        // Seed portfolios
        var p1 = new Portfolio
        {
            PortfolioId = 1,
            UserId = 1,
            PortfolioName = "User 1 Alpha Growth",
            Description = "Primary aggressive growth portfolio"
        };
        var p2 = new Portfolio
        {
            PortfolioId = 2,
            UserId = 1,
            PortfolioName = "User 1 Retirement Dividends",
            Description = "Passive dividend collection"
        };
        var pOther = new Portfolio
        {
            PortfolioId = 3,
            UserId = 2,
            PortfolioName = "Secret Confidential Portfolio",
            Description = "Private to User 2"
        };
        context.Portfolios.AddRange(p1, p2, pOther);

        // Seed watchlists
        var w1 = new Watchlist { WatchlistId = 1, UserId = 1, WatchlistName = "Blue Chips" };
        var wItem1 = new WatchlistItem { WatchlistId = 1, CompanyId = 1, TargetPrice = 300m, Watchlist = w1, Company = c1 };
        context.Watchlists.Add(w1);
        context.WatchlistItems.Add(wItem1);

        var wOther = new Watchlist { WatchlistId = 2, UserId = 2, WatchlistName = "User 2 Watchlist" };
        var wItemOther = new WatchlistItem { WatchlistId = 2, CompanyId = 3, TargetPrice = 230m, Watchlist = wOther, Company = c3 };
        context.Watchlists.Add(wOther);
        context.WatchlistItems.Add(wItemOther);

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task Search_ByCompanyName_ReturnsMatchingCompanies()
    {
        using var context = CreateInMemoryDbContext();
        var service = new SearchService(context);

        var res = await service.SearchAsync("Grameenphone", userId: 1);

        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        Assert.NotEmpty(res.Data.Companies);
        Assert.Contains(res.Data.Companies, c => c.Id == 1 && c.Title.Contains("GP"));
    }

    [Fact]
    public async Task Search_ByTicker_ReturnsMatchingCompanies()
    {
        using var context = CreateInMemoryDbContext();
        var service = new SearchService(context);

        var res = await service.SearchAsync("BEXIMCO", userId: 1);

        Assert.True(res.Success);
        Assert.NotEmpty(res.Data!.Companies);
        Assert.Equal(2, res.Data.Companies.First().Id);
        Assert.Contains("BEXIMCO", res.Data.Companies.First().Title);
    }

    [Fact]
    public async Task Search_BySectorName_ReturnsMatchingSectors()
    {
        using var context = CreateInMemoryDbContext();
        var service = new SearchService(context);

        var res = await service.SearchAsync("Pharma", userId: 1);

        Assert.True(res.Success);
        Assert.NotEmpty(res.Data!.Sectors);
        Assert.Contains(res.Data.Sectors, s => s.Title == "Pharmaceuticals");
    }

    [Fact]
    public async Task Search_PrivatePortfolioIsolation_NeverExposesOtherUserData()
    {
        using var context = CreateInMemoryDbContext();
        var service = new SearchService(context);

        // Searching for User 2's portfolio by exact term as User 1
        var res = await service.SearchAsync("Confidential", userId: 1);

        Assert.True(res.Success);
        // Must be empty for User 1
        Assert.Empty(res.Data!.Portfolios);

        // User 2 searching should find it
        var resUser2 = await service.SearchAsync("Confidential", userId: 2);
        Assert.NotEmpty(resUser2.Data!.Portfolios);
        Assert.Equal(3, resUser2.Data.Portfolios.First().Id);
    }

    [Fact]
    public async Task Search_UserWatchlist_ReturnsOnlyOwnWatchlistItems()
    {
        using var context = CreateInMemoryDbContext();
        var service = new SearchService(context);

        var res = await service.SearchAsync("Blue Chips", userId: 1);

        Assert.True(res.Success);
        Assert.NotEmpty(res.Data!.Watchlist);
        Assert.Contains(res.Data.Watchlist, w => w.Id == 1);

        // User 2's watchlist item should not be visible to User 1
        var resUser2Item = await service.SearchAsync("User 2 Watchlist", userId: 1);
        Assert.Empty(resUser2Item.Data!.Watchlist);
    }

    [Fact]
    public async Task Search_NoResults_ReturnsEmptyListsWithZeroTotalCount()
    {
        using var context = CreateInMemoryDbContext();
        var service = new SearchService(context);

        var res = await service.SearchAsync("NonExistentQuery9999XYZ", userId: 1);

        Assert.True(res.Success);
        Assert.Equal(0, res.Data!.TotalCount);
        Assert.Empty(res.Data.Companies);
        Assert.Empty(res.Data.Sectors);
        Assert.Empty(res.Data.Portfolios);
        Assert.Empty(res.Data.Watchlist);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Search_EmptyQuery_ReturnsSafelyWithoutErrors(string? query)
    {
        using var context = CreateInMemoryDbContext();
        var service = new SearchService(context);

        var res = await service.SearchAsync(query, userId: 1);

        Assert.True(res.Success);
        Assert.Equal(0, res.Data!.TotalCount);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("' OR '1'='1")]
    [InlineData("\"")]
    [InlineData("`")]
    [InlineData("&")]
    [InlineData("<script>")]
    public async Task Search_SpecialCharacters_ExecutesSafelyWithoutExceptions(string specialQuery)
    {
        using var context = CreateInMemoryDbContext();
        var service = new SearchService(context);

        var res = await service.SearchAsync(specialQuery, userId: 1);

        Assert.True(res.Success);
        Assert.NotNull(res.Data);
    }
}
