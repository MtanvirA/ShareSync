using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.DTOs.Portfolios;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ShareSync.Tests;

public class PortfolioTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ShareSyncDbContext(options);
    }

    [Fact]
    public async Task CreatePortfolio_WithValidData_ReturnsSuccessAndPersists()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioService(context);
        const int userId = 1;

        var request = new CreatePortfolioRequestDto
        {
            PortfolioName = "Retirement Fund",
            Description = "Long-term low-risk assets"
        };

        // Act
        var response = await service.CreatePortfolioAsync(request, userId);

        // Assert
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("Retirement Fund", response.Data.PortfolioName);
        Assert.Equal("Long-term low-risk assets", response.Data.Description);

        var saved = await context.Portfolios.FirstOrDefaultAsync(p => p.PortfolioName == "Retirement Fund");
        Assert.NotNull(saved);
        Assert.Equal(userId, saved.UserId);
    }

    [Fact]
    public async Task CreatePortfolio_WithDuplicateNameForSameUser_ThrowsConflictException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioService(context);
        const int userId = 1;

        await service.CreatePortfolioAsync(new CreatePortfolioRequestDto
        {
            PortfolioName = "Growth Portfolio"
        }, userId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppException>(() => service.CreatePortfolioAsync(new CreatePortfolioRequestDto
        {
            PortfolioName = "GROWTH PORTFOLIO" // Case-insensitive duplicate check
        }, userId));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task CreatePortfolio_WithSameNameForDifferentUser_Succeeds()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioService(context);

        // User 1 creates "Growth"
        var user1Response = await service.CreatePortfolioAsync(new CreatePortfolioRequestDto
        {
            PortfolioName = "Growth"
        }, userId: 1);

        // User 2 creates "Growth" (composite unique constraint allows same name across different users)
        var user2Response = await service.CreatePortfolioAsync(new CreatePortfolioRequestDto
        {
            PortfolioName = "Growth"
        }, userId: 2);

        // Assert
        Assert.True(user1Response.Success);
        Assert.True(user2Response.Success);
        Assert.NotEqual(user1Response.Data!.PortfolioId, user2Response.Data!.PortfolioId);
    }

    [Fact]
    public async Task GetUserPortfolios_ReturnsOnlyPortfoliosOwnedByCurrentUser()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioService(context);

        // Seed portfolios for User 1
        await service.CreatePortfolioAsync(new CreatePortfolioRequestDto { PortfolioName = "User 1 - Portfolio A" }, userId: 1);
        await service.CreatePortfolioAsync(new CreatePortfolioRequestDto { PortfolioName = "User 1 - Portfolio B" }, userId: 1);

        // Seed portfolio for User 2
        await service.CreatePortfolioAsync(new CreatePortfolioRequestDto { PortfolioName = "User 2 - Secret Portfolio" }, userId: 2);

        // Act
        var user1Portfolios = await service.GetUserPortfoliosAsync(userId: 1);
        var user2Portfolios = await service.GetUserPortfoliosAsync(userId: 2);

        // Assert
        Assert.Equal(2, user1Portfolios.Data!.Count);
        Assert.All(user1Portfolios.Data, p => Assert.DoesNotContain("User 2", p.PortfolioName));

        Assert.Single(user2Portfolios.Data!);
        Assert.Equal("User 2 - Secret Portfolio", user2Portfolios.Data![0].PortfolioName);
    }

    [Fact]
    public async Task GetPortfolioById_WhenOwnedByCurrentUser_ReturnsDetailWithHoldings()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioService(context);
        const int userId = 1;

        var company = new Company
        {
            CompanyName = "Test Tech PLC",
            TickerSymbol = "TTECH",
            SectorId = 1,
            CurrentPrice = 150.00m,
            MarketCap = 1000000000m
        };
        context.Companies.Add(company);
        await context.SaveChangesAsync();

        var created = await service.CreatePortfolioAsync(new CreatePortfolioRequestDto { PortfolioName = "Tech Portfolio" }, userId);
        var portfolioId = created.Data!.PortfolioId;

        // Add 2 BUY transactions and 1 SELL transaction
        // BUY 100 @ 100 = 10,000
        // BUY 50 @ 130 = 6,500
        // Total Buy = 150 shares for 16,500 -> Weighted Avg Buy Price = 110.00
        // SELL 30 -> Net shares = 120
        // Current Price = 150 -> Market Value = 120 * 150 = 18,000
        // Cost Basis = 120 * 110 = 13,200
        // Unrealized P/L = 18,000 - 13,200 = +4,800 (+36.36%)
        context.Transactions.AddRange(
            new Transaction { PortfolioId = portfolioId, CompanyId = company.CompanyId, TransactionType = "BUY", Quantity = 100, PricePerShare = 100.00m, TransactionDate = DateTime.UtcNow },
            new Transaction { PortfolioId = portfolioId, CompanyId = company.CompanyId, TransactionType = "BUY", Quantity = 50, PricePerShare = 130.00m, TransactionDate = DateTime.UtcNow },
            new Transaction { PortfolioId = portfolioId, CompanyId = company.CompanyId, TransactionType = "SELL", Quantity = 30, PricePerShare = 140.00m, TransactionDate = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        // Act
        var detail = await service.GetPortfolioByIdAsync(portfolioId, userId);

        // Assert
        Assert.True(detail.Success);
        Assert.NotNull(detail.Data);
        Assert.Equal(18000.00m, detail.Data.TotalValue);
        Assert.Equal(13200.00m, detail.Data.TotalInvested);
        Assert.Equal(4800.00m, detail.Data.UnrealizedProfitLoss);
        Assert.Single(detail.Data.Holdings);

        var holding = detail.Data.Holdings[0];
        Assert.Equal("TTECH", holding.TickerSymbol);
        Assert.Equal(120m, holding.Shares);
        Assert.Equal(110.00m, holding.AverageBuyPrice);
        Assert.Equal(150.00m, holding.CurrentPrice);
        Assert.Equal(18000.00m, holding.MarketValue);
        Assert.Equal(4800.00m, holding.UnrealizedProfitLoss);
    }

    [Fact]
    public async Task GetPortfolioById_WhenOwnedByAnotherUser_ThrowsForbiddenException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioService(context);

        var created = await service.CreatePortfolioAsync(new CreatePortfolioRequestDto { PortfolioName = "User 1 Private Portfolio" }, userId: 1);
        var user1PortfolioId = created.Data!.PortfolioId;

        // Act & Assert - User 2 attempts to view User 1's portfolio
        var ex = await Assert.ThrowsAsync<AppException>(() => service.GetPortfolioByIdAsync(user1PortfolioId, userId: 2));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task GetPortfolioById_WhenNonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioService(context);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.GetPortfolioByIdAsync(99999, userId: 1));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task UpdatePortfolio_WhenOwnedByCurrentUser_UpdatesNameAndDescription()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioService(context);
        const int userId = 1;

        var created = await service.CreatePortfolioAsync(new CreatePortfolioRequestDto
        {
            PortfolioName = "Initial Name",
            Description = "Initial Description"
        }, userId);

        // Act
        var updateResponse = await service.UpdatePortfolioAsync(created.Data!.PortfolioId, new UpdatePortfolioRequestDto
        {
            PortfolioName = "Updated Name",
            Description = "Updated Description"
        }, userId);

        // Assert
        Assert.True(updateResponse.Success);
        Assert.Equal("Updated Name", updateResponse.Data!.PortfolioName);
        Assert.Equal("Updated Description", updateResponse.Data.Description);

        var saved = await context.Portfolios.FindAsync(created.Data.PortfolioId);
        Assert.NotNull(saved);
        Assert.Equal("Updated Name", saved.PortfolioName);
        Assert.Equal("Updated Description", saved.Description);
    }

    [Fact]
    public async Task UpdatePortfolio_WhenOwnedByAnotherUser_ThrowsForbiddenException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioService(context);

        var created = await service.CreatePortfolioAsync(new CreatePortfolioRequestDto { PortfolioName = "User 1 Asset" }, userId: 1);
        var portfolioId = created.Data!.PortfolioId;

        // Act & Assert - User 2 tries to update User 1's portfolio
        var ex = await Assert.ThrowsAsync<AppException>(() => service.UpdatePortfolioAsync(portfolioId, new UpdatePortfolioRequestDto
        {
            PortfolioName = "Hacked Name"
        }, userId: 2));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task DeletePortfolio_WhenSafeWithoutTransactions_DeletesSuccessfully()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioService(context);
        const int userId = 1;

        var created = await service.CreatePortfolioAsync(new CreatePortfolioRequestDto { PortfolioName = "Empty Portfolio" }, userId);
        var portfolioId = created.Data!.PortfolioId;

        // Act
        var deleteResponse = await service.DeletePortfolioAsync(portfolioId, userId);

        // Assert
        Assert.True(deleteResponse.Success);
        var found = await context.Portfolios.FindAsync(portfolioId);
        Assert.Null(found);
    }

    [Fact]
    public async Task DeletePortfolio_WhenContainsActiveTransactions_ThrowsBusinessRuleException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioService(context);
        const int userId = 1;

        var created = await service.CreatePortfolioAsync(new CreatePortfolioRequestDto { PortfolioName = "Active Portfolio" }, userId);
        var portfolioId = created.Data!.PortfolioId;

        // Add transaction
        context.Transactions.Add(new Transaction
        {
            PortfolioId = portfolioId,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 50,
            PricePerShare = 100,
            TransactionDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        // Act & Assert - Cannot delete portfolio with transactions (financial history preserved)
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeletePortfolioAsync(portfolioId, userId));
        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("active transactions", ex.Message);
    }

    [Fact]
    public async Task DeletePortfolio_WhenOwnedByAnotherUser_ThrowsForbiddenException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioService(context);

        var created = await service.CreatePortfolioAsync(new CreatePortfolioRequestDto { PortfolioName = "User 1 Portfolio" }, userId: 1);
        var portfolioId = created.Data!.PortfolioId;

        // Act & Assert - User 2 attempts to delete User 1's portfolio
        var ex = await Assert.ThrowsAsync<AppException>(() => service.DeletePortfolioAsync(portfolioId, userId: 2));
        Assert.Equal(403, ex.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreatePortfolio_WithEmptyOrWhitespaceName_ThrowsBadRequest(string? invalidName)
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioService(context);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppException>(() => service.CreatePortfolioAsync(new CreatePortfolioRequestDto
        {
            PortfolioName = invalidName!
        }, userId: 1));

        Assert.Equal(400, ex.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task UpdatePortfolio_WithEmptyOrWhitespaceName_ThrowsBadRequest(string? invalidName)
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioService(context);

        var created = await service.CreatePortfolioAsync(new CreatePortfolioRequestDto { PortfolioName = "Valid Name" }, userId: 1);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppException>(() => service.UpdatePortfolioAsync(created.Data!.PortfolioId, new UpdatePortfolioRequestDto
        {
            PortfolioName = invalidName!
        }, userId: 1));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CreatePortfolio_WithSqlInjectionPayload_IsSafelyStoredAsLiteralText()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioService(context);
        const string sqlPayload = "'; DROP TABLE PORTFOLIOS; --";
        const string sqlDesc = "' OR 1=1; --";

        // Act
        var result = await service.CreatePortfolioAsync(new CreatePortfolioRequestDto
        {
            PortfolioName = sqlPayload,
            Description = sqlDesc
        }, userId: 1);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(sqlPayload, result.Data!.PortfolioName);

        // Verify the database table still exists and data was literally saved without executing commands
        var saved = await context.Portfolios.FindAsync(result.Data.PortfolioId);
        Assert.NotNull(saved);
        Assert.Equal(sqlPayload, saved.PortfolioName);
        Assert.Equal(sqlDesc, saved.Description);
    }
}

