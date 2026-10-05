using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.DTOs.Companies;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class CompanyDetailTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ShareSyncDbContext(options);
    }

    [Fact]
    public async Task GetCompanyDetail_WithValidCompanyAndUserHolding_ReturnsCompanyAndUserPosition()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        int userId = 1;

        var sector = new Sector { SectorId = 1, SectorName = "Telecommunication" };
        var company = new Company
        {
            CompanyId = 1,
            TickerSymbol = "GP",
            CompanyName = "Grameenphone PLC",
            CurrentPrice = 250.00m,
            MarketCap = 30000000m,
            SectorId = 1,
            Sector = sector
        };
        var portfolio = new Portfolio
        {
            PortfolioId = 10,
            UserId = userId,
            PortfolioName = "Primary Growth"
        };
        var txBuy = new Transaction
        {
            TransactionId = 100,
            PortfolioId = 10,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 200.00m,
            TransactionDate = DateTime.UtcNow.AddDays(-10),
            Company = company,
            Portfolio = portfolio
        };
        var dividend = new Dividend
        {
            DividendId = 50,
            CompanyId = 1,
            DividendPerShare = 10.00m,
            DeclarationDate = DateTime.UtcNow.AddDays(-20),
            PaymentDate = DateTime.UtcNow.AddDays(-5),
            Company = company
        };
        var watchlist = new Watchlist
        {
            WatchlistId = 5,
            UserId = userId,
            WatchlistName = "Blue Chips"
        };
        var watchlistItem = new WatchlistItem
        {
            WatchlistId = 5,
            CompanyId = 1,
            TargetPrice = 260.00m,
            Watchlist = watchlist,
            Company = company
        };

        context.Sectors.Add(sector);
        context.Companies.Add(company);
        context.Portfolios.Add(portfolio);
        context.Transactions.Add(txBuy);
        context.Dividends.Add(dividend);
        context.Watchlists.Add(watchlist);
        context.WatchlistItems.Add(watchlistItem);
        await context.SaveChangesAsync();

        var service = new CompanyService(context);

        // Act
        var result = await service.GetCompanyDetailAsync(1, userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        var d = result.Data;
        Assert.NotNull(d);
        Assert.Equal("GP", d.TickerSymbol);
        Assert.Equal("Grameenphone PLC", d.CompanyName);
        Assert.Equal("Telecommunication", d.SectorName);
        Assert.Equal(250.00m, d.CurrentPrice);

        // Holding assertions
        Assert.True(d.UserOwnsShares);
        Assert.NotNull(d.UserHolding);
        Assert.Equal(100, d.UserHolding.Shares);
        Assert.Equal(200.00m, d.UserHolding.AverageBuyPrice);
        Assert.Equal(20000.00m, d.UserHolding.CostBasis);
        Assert.Equal(25000.00m, d.UserHolding.CurrentMarketValue);
        Assert.Equal(5000.00m, d.UserHolding.UnrealizedProfitLoss);
        Assert.Equal(25.00m, d.UserHolding.ReturnPercentage);

        // Watchlist assertions
        Assert.True(d.IsInWatchlist);
        Assert.Equal(5, d.WatchlistId);
        Assert.Equal("Blue Chips", d.WatchlistName);
        Assert.Equal(260.00m, d.TargetPrice);
        Assert.False(d.IsTargetReached); // 250 < 260

        // Dividend assertions
        Assert.Single(d.Dividends);
        Assert.Equal(10.00m, d.Dividends[0].DividendPerShare);
        Assert.Equal(1000.00m, d.Dividends[0].EstimatedIncome); // 100 shares * 10.00
    }

    [Fact]
    public async Task GetCompanyDetail_WithNoHolding_ReturnsCompanyWithNullHolding()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        int userId = 2;

        var sector = new Sector { SectorId = 2, SectorName = "Pharmaceuticals" };
        var company = new Company
        {
            CompanyId = 2,
            TickerSymbol = "SQURPHARMA",
            CompanyName = "Square Pharmaceuticals",
            CurrentPrice = 220.00m,
            SectorId = 2,
            Sector = sector
        };

        context.Sectors.Add(sector);
        context.Companies.Add(company);
        await context.SaveChangesAsync();

        var service = new CompanyService(context);

        // Act
        var result = await service.GetCompanyDetailAsync(2, userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        var d = result.Data;
        Assert.NotNull(d);
        Assert.Equal("SQURPHARMA", d.TickerSymbol);
        Assert.False(d.UserOwnsShares);
        Assert.Null(d.UserHolding);
        Assert.False(d.IsInWatchlist);
        Assert.Empty(d.Dividends);
    }

    [Fact]
    public async Task GetCompanyDetail_WithNonExistentCompany_ThrowsNotFoundException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new CompanyService(context);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetCompanyDetailAsync(999, 1));
    }

    [Fact]
    public async Task GetCompanyDetail_UserIsolation_DoesNotExposeAnotherUsersHoldingsOrWatchlist()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        int userA = 1;
        int userB = 2;

        var sector = new Sector { SectorId = 1, SectorName = "Fuel" };
        var company = new Company
        {
            CompanyId = 3,
            TickerSymbol = "MJLBD",
            CompanyName = "MJL Bangladesh",
            CurrentPrice = 90.00m,
            SectorId = 1,
            Sector = sector
        };
        var portfolioB = new Portfolio
        {
            PortfolioId = 20,
            UserId = userB,
            PortfolioName = "User B Secret Fund"
        };
        var txB = new Transaction
        {
            TransactionId = 200,
            PortfolioId = 20,
            CompanyId = 3,
            TransactionType = "BUY",
            Quantity = 500,
            PricePerShare = 80.00m,
            TransactionDate = DateTime.UtcNow,
            Company = company,
            Portfolio = portfolioB
        };
        var watchlistB = new Watchlist
        {
            WatchlistId = 8,
            UserId = userB,
            WatchlistName = "User B Watchlist"
        };
        var watchlistItemB = new WatchlistItem
        {
            WatchlistId = 8,
            CompanyId = 3,
            TargetPrice = 120.00m,
            Watchlist = watchlistB,
            Company = company
        };

        context.Sectors.Add(sector);
        context.Companies.Add(company);
        context.Portfolios.Add(portfolioB);
        context.Transactions.Add(txB);
        context.Watchlists.Add(watchlistB);
        context.WatchlistItems.Add(watchlistItemB);
        await context.SaveChangesAsync();

        var service = new CompanyService(context);

        // Act: User A queries company 3
        var resultA = await service.GetCompanyDetailAsync(3, userA);

        // Assert: User A sees public company info, but NO holdings or watchlist
        Assert.NotNull(resultA.Data);
        Assert.Equal("MJLBD", resultA.Data.TickerSymbol);
        Assert.False(resultA.Data.UserOwnsShares);
        Assert.Null(resultA.Data.UserHolding);
        Assert.False(resultA.Data.IsInWatchlist);
        Assert.Null(resultA.Data.WatchlistName);
    }
}
