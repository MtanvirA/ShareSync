using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.DTOs.Simulator;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class SimulatorTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ShareSyncDbContext(options);
    }

    [Fact]
    public async Task Simulate_ValidBuyNewStock_CalculatesCorrectSimulatedHoldingsAndAllocation()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        const int userId = 1;
        const int portfolioId = 10;
        const int companyAId = 101;
        const int companyBId = 102;

        var user = new AppUser { UserId = userId, Name = "Investor One", Email = "investor@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        var sector = new Sector { SectorId = 1, SectorName = "Pharmaceuticals" };
        var companyA = new Company { CompanyId = companyAId, CompanyName = "Square Pharma", TickerSymbol = "SQURPHARMA", SectorId = 1, CurrentPrice = 250.00m, Sector = sector };
        var companyB = new Company { CompanyId = companyBId, CompanyName = "Beximco Pharma", TickerSymbol = "BXPHARMA", SectorId = 1, CurrentPrice = 120.00m, Sector = sector };
        var portfolio = new Portfolio { PortfolioId = portfolioId, UserId = userId, PortfolioName = "Growth Portfolio", User = user };

        context.Users.Add(user);
        context.Sectors.Add(sector);
        context.Companies.AddRange(companyA, companyB);
        context.Portfolios.Add(portfolio);

        // Existing holding: 100 shares of SQURPHARMA @ 200 (Total buy cost = 20,000, market value = 25,000)
        context.Transactions.Add(new Transaction
        {
            TransactionId = 1,
            PortfolioId = portfolioId,
            CompanyId = companyAId,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 200.00m,
            TransactionDate = DateTime.UtcNow.AddDays(-10),
            Company = companyA,
            Portfolio = portfolio
        });
        await context.SaveChangesAsync();

        var service = new SimulatorService(context);

        // Act: Simulate BUY 100 shares of BXPHARMA at hypothetical price 125.00
        var request = new SimulateTransactionRequestDto
        {
            PortfolioId = portfolioId,
            CompanyId = companyBId,
            TransactionType = "BUY",
            Quantity = 100,
            HypotheticalPrice = 125.00m
        };

        var response = await service.SimulateTransactionAsync(request, userId);

        // Assert
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        var res = response.Data;

        // Current position for BXPHARMA was 0
        Assert.Equal(0, res.CurrentHoldingQuantity);
        Assert.Equal(0, res.CurrentAverageCost);
        Assert.Equal(0, res.CurrentPositionMarketValue);
        Assert.Equal(0, res.CurrentAllocationPercentage);

        // Simulated position for BXPHARMA: 100 shares @ avg cost 125.00
        Assert.Equal(100, res.SimulatedHoldingQuantity);
        Assert.Equal(125.00m, res.SimulatedAverageCost);
        Assert.Equal(12500.00m, res.SimulatedPositionCostBasis);
        // Market price is 120.00 -> Market value = 100 * 120 = 12,000
        Assert.Equal(12000.00m, res.SimulatedPositionMarketValue);
        // Unrealized PL = 12,000 - 12,500 = -500
        Assert.Equal(-500.00m, res.SimulatedUnrealizedProfitLoss);
        Assert.Equal(-4.00m, res.SimulatedUnrealizedProfitLossPercentage);

        // Portfolio level:
        // Current portfolio total value: SQURPHARMA (100 * 250 = 25,000)
        Assert.Equal(25000.00m, res.CurrentPortfolioTotalValue);
        // Simulated portfolio total value: 25,000 + 12,000 = 37,000
        Assert.Equal(37000.00m, res.SimulatedPortfolioTotalValue);
        Assert.Equal(12000.00m, res.PortfolioTotalValueChange);

        // Simulated allocation: 12,000 / 37,000 = 32.43%
        Assert.Equal(32.43m, res.SimulatedAllocationPercentage);
        Assert.True(res.IsSimulation);
    }

    [Fact]
    public async Task Simulate_ValidBuyAdditionalShares_CalculatesAuthoritativeWeightedAverageCost()
    {
        // Arrange: Existing holding: 100 shares @ 200 (Cost = 20,000)
        using var context = CreateInMemoryDbContext();
        const int userId = 1;
        const int portfolioId = 10;
        const int companyId = 101;

        var user = new AppUser { UserId = userId, Name = "Investor One", Email = "investor@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        var sector = new Sector { SectorId = 1, SectorName = "Telecom" };
        var company = new Company { CompanyId = companyId, CompanyName = "Grameenphone", TickerSymbol = "GP", SectorId = 1, CurrentPrice = 300.00m, Sector = sector };
        var portfolio = new Portfolio { PortfolioId = portfolioId, UserId = userId, PortfolioName = "Main Portfolio", User = user };

        context.Users.Add(user);
        context.Sectors.Add(sector);
        context.Companies.Add(company);
        context.Portfolios.Add(portfolio);

        context.Transactions.Add(new Transaction
        {
            TransactionId = 1,
            PortfolioId = portfolioId,
            CompanyId = companyId,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 200.00m,
            TransactionDate = DateTime.UtcNow.AddDays(-10),
            Company = company,
            Portfolio = portfolio
        });
        await context.SaveChangesAsync();

        var service = new SimulatorService(context);

        // Act: Simulate BUY 100 shares @ 250
        // New total buy qty = 200, total buy cost = 20,000 + 25,000 = 45,000.
        // New WABP = 45,000 / 200 = 225.00
        var request = new SimulateTransactionRequestDto
        {
            PortfolioId = portfolioId,
            CompanyId = companyId,
            TransactionType = "BUY",
            Quantity = 100,
            HypotheticalPrice = 250.00m
        };

        var response = await service.SimulateTransactionAsync(request, userId);

        // Assert
        Assert.True(response.Success);
        var res = response.Data!;

        Assert.Equal(100, res.CurrentHoldingQuantity);
        Assert.Equal(200.00m, res.CurrentAverageCost);
        Assert.Equal(200, res.SimulatedHoldingQuantity);
        Assert.Equal(225.00m, res.SimulatedAverageCost);
        Assert.Equal(25.00m, res.AverageCostChange);

        // Market price = 300 -> Position market value = 200 * 300 = 60,000
        Assert.Equal(60000.00m, res.SimulatedPositionMarketValue);
        // Cost basis = 200 * 225 = 45,000
        Assert.Equal(45000.00m, res.SimulatedPositionCostBasis);
        // Unrealized PL = 60,000 - 45,000 = 15,000
        Assert.Equal(15000.00m, res.SimulatedUnrealizedProfitLoss);
    }

    [Fact]
    public async Task Simulate_ValidSell_MaintainsAverageCostAndCalculatesRealizedGain()
    {
        // Arrange: Existing holding: 100 shares @ 200
        using var context = CreateInMemoryDbContext();
        const int userId = 1;
        const int portfolioId = 10;
        const int companyId = 101;

        var user = new AppUser { UserId = userId, Name = "Investor One", Email = "investor@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        var sector = new Sector { SectorId = 1, SectorName = "Banking" };
        var company = new Company { CompanyId = companyId, CompanyName = "BRAC Bank", TickerSymbol = "BRACBANK", SectorId = 1, CurrentPrice = 240.00m, Sector = sector };
        var portfolio = new Portfolio { PortfolioId = portfolioId, UserId = userId, PortfolioName = "Main Portfolio", User = user };

        context.Users.Add(user);
        context.Sectors.Add(sector);
        context.Companies.Add(company);
        context.Portfolios.Add(portfolio);

        context.Transactions.Add(new Transaction
        {
            TransactionId = 1,
            PortfolioId = portfolioId,
            CompanyId = companyId,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 200.00m,
            TransactionDate = DateTime.UtcNow.AddDays(-10),
            Company = company,
            Portfolio = portfolio
        });
        await context.SaveChangesAsync();

        var service = new SimulatorService(context);

        // Act: Simulate SELL 40 shares @ 260.00
        // Realized gain = (260 - 200) * 40 = 2,400.00
        // Remaining qty = 60 shares @ avg cost 200.00
        var request = new SimulateTransactionRequestDto
        {
            PortfolioId = portfolioId,
            CompanyId = companyId,
            TransactionType = "SELL",
            Quantity = 40,
            HypotheticalPrice = 260.00m
        };

        var response = await service.SimulateTransactionAsync(request, userId);

        // Assert
        Assert.True(response.Success);
        var res = response.Data!;

        Assert.Equal(100, res.CurrentHoldingQuantity);
        Assert.Equal(60, res.SimulatedHoldingQuantity);
        Assert.Equal(200.00m, res.CurrentAverageCost);
        Assert.Equal(200.00m, res.SimulatedAverageCost); // WABP unchanged
        Assert.Equal(2400.00m, res.RealizedProfitLoss);
        Assert.Equal(-40, res.QuantityChange);
    }

    [Fact]
    public async Task Simulate_SellExceedingAvailableHoldings_ThrowsBusinessRuleException()
    {
        // Arrange: Existing holding: 50 shares
        using var context = CreateInMemoryDbContext();
        const int userId = 1;
        const int portfolioId = 10;
        const int companyId = 101;

        var user = new AppUser { UserId = userId, Name = "Investor One", Email = "investor@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        var sector = new Sector { SectorId = 1, SectorName = "Banking" };
        var company = new Company { CompanyId = companyId, CompanyName = "BRAC Bank", TickerSymbol = "BRACBANK", SectorId = 1, CurrentPrice = 240.00m, Sector = sector };
        var portfolio = new Portfolio { PortfolioId = portfolioId, UserId = userId, PortfolioName = "Main Portfolio", User = user };

        context.Users.Add(user);
        context.Sectors.Add(sector);
        context.Companies.Add(company);
        context.Portfolios.Add(portfolio);

        context.Transactions.Add(new Transaction
        {
            TransactionId = 1,
            PortfolioId = portfolioId,
            CompanyId = companyId,
            TransactionType = "BUY",
            Quantity = 50,
            PricePerShare = 200.00m,
            Company = company,
            Portfolio = portfolio
        });
        await context.SaveChangesAsync();

        var service = new SimulatorService(context);

        // Act & Assert: Attempt to sell 75 shares when only 50 available
        var request = new SimulateTransactionRequestDto
        {
            PortfolioId = portfolioId,
            CompanyId = companyId,
            TransactionType = "SELL",
            Quantity = 75,
            HypotheticalPrice = 250.00m
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.SimulateTransactionAsync(request, userId));
        Assert.Contains("exceeds available holdings", ex.Message);
    }

    [Fact]
    public async Task Simulate_InvalidQuantityOrPrice_ThrowsAppException()
    {
        using var context = CreateInMemoryDbContext();
        const int userId = 1;
        const int portfolioId = 10;
        const int companyId = 101;

        var user = new AppUser { UserId = userId, Name = "Investor", Email = "investor@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        var company = new Company { CompanyId = companyId, CompanyName = "Test Co", TickerSymbol = "TEST", CurrentPrice = 100m };
        var portfolio = new Portfolio { PortfolioId = portfolioId, UserId = userId, PortfolioName = "P1", User = user };

        context.Users.Add(user);
        context.Companies.Add(company);
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync();

        var service = new SimulatorService(context);

        // Zero Quantity
        var reqZeroQty = new SimulateTransactionRequestDto { PortfolioId = portfolioId, CompanyId = companyId, TransactionType = "BUY", Quantity = 0, HypotheticalPrice = 100m };
        await Assert.ThrowsAsync<AppException>(() => service.SimulateTransactionAsync(reqZeroQty, userId));

        // Negative Price
        var reqNegPrice = new SimulateTransactionRequestDto { PortfolioId = portfolioId, CompanyId = companyId, TransactionType = "BUY", Quantity = 10, HypotheticalPrice = -50m };
        await Assert.ThrowsAsync<AppException>(() => service.SimulateTransactionAsync(reqNegPrice, userId));
    }

    [Fact]
    public async Task Simulate_NonexistentCompany_ThrowsNotFoundException()
    {
        using var context = CreateInMemoryDbContext();
        const int userId = 1;
        const int portfolioId = 10;

        var user = new AppUser { UserId = userId, Name = "Investor", Email = "investor@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        var portfolio = new Portfolio { PortfolioId = portfolioId, UserId = userId, PortfolioName = "P1", User = user };

        context.Users.Add(user);
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync();

        var service = new SimulatorService(context);

        var request = new SimulateTransactionRequestDto { PortfolioId = portfolioId, CompanyId = 99999, TransactionType = "BUY", Quantity = 10, HypotheticalPrice = 100m };
        await Assert.ThrowsAsync<NotFoundException>(() => service.SimulateTransactionAsync(request, userId));
    }

    [Fact]
    public async Task Simulate_PortfolioBelongingToAnotherUser_ThrowsForbidden()
    {
        using var context = CreateInMemoryDbContext();
        const int ownerUserId = 1;
        const int unauthorizedUserId = 2;
        const int portfolioId = 10;
        const int companyId = 101;

        var owner = new AppUser { UserId = ownerUserId, Name = "Owner", Email = "owner@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        var company = new Company { CompanyId = companyId, CompanyName = "Test Co", TickerSymbol = "TEST", CurrentPrice = 100m };
        var portfolio = new Portfolio { PortfolioId = portfolioId, UserId = ownerUserId, PortfolioName = "Owner Portfolio", User = owner };

        context.Users.Add(owner);
        context.Companies.Add(company);
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync();

        var service = new SimulatorService(context);

        var request = new SimulateTransactionRequestDto { PortfolioId = portfolioId, CompanyId = companyId, TransactionType = "BUY", Quantity = 10, HypotheticalPrice = 100m };
        var ex = await Assert.ThrowsAsync<AppException>(() => service.SimulateTransactionAsync(request, unauthorizedUserId));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Simulate_IsStrictlyNonDestructive_LeavesRealTransactionsAndHoldingsUnchanged()
    {
        using var context = CreateInMemoryDbContext();
        const int userId = 1;
        const int portfolioId = 10;
        const int companyId = 101;

        var user = new AppUser { UserId = userId, Name = "Owner", Email = "owner@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        var company = new Company { CompanyId = companyId, CompanyName = "Test Co", TickerSymbol = "TEST", CurrentPrice = 100m };
        var portfolio = new Portfolio { PortfolioId = portfolioId, UserId = userId, PortfolioName = "Owner Portfolio", User = user };

        context.Users.Add(user);
        context.Companies.Add(company);
        context.Portfolios.Add(portfolio);
        context.Transactions.Add(new Transaction
        {
            TransactionId = 1,
            PortfolioId = portfolioId,
            CompanyId = companyId,
            TransactionType = "BUY",
            Quantity = 50,
            PricePerShare = 90.00m,
            Company = company,
            Portfolio = portfolio
        });
        await context.SaveChangesAsync();

        var txCountBefore = await context.Transactions.CountAsync();
        var service = new SimulatorService(context);

        // Run simulation
        var request = new SimulateTransactionRequestDto { PortfolioId = portfolioId, CompanyId = companyId, TransactionType = "BUY", Quantity = 100, HypotheticalPrice = 150m };
        var response = await service.SimulateTransactionAsync(request, userId);

        Assert.True(response.Success);

        // Verify database records are 100% unaltered
        var txCountAfter = await context.Transactions.CountAsync();
        Assert.Equal(txCountBefore, txCountAfter);

        var originalTx = await context.Transactions.FirstAsync();
        Assert.Equal(50, originalTx.Quantity);
        Assert.Equal(90.00m, originalTx.PricePerShare);
    }

    [Fact]
    public async Task SimulatorController_Simulate_ReturnsOkWithSimulatedResult()
    {
        using var context = CreateInMemoryDbContext();
        const int userId = 1;
        const int portfolioId = 10;
        const int companyId = 101;

        var user = new AppUser { UserId = userId, Name = "Owner", Email = "owner@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        var company = new Company { CompanyId = companyId, CompanyName = "Test Co", TickerSymbol = "TEST", CurrentPrice = 100m };
        var portfolio = new Portfolio { PortfolioId = portfolioId, UserId = userId, PortfolioName = "Owner Portfolio", User = user };

        context.Users.Add(user);
        context.Companies.Add(company);
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync();

        var service = new SimulatorService(context);
        var fakeUser = new FakeCurrentUserService(userId: 1);
        var controller = new ShareSync.Web.Controllers.SimulatorController(service, fakeUser);

        var request = new SimulateTransactionRequestDto
        {
            PortfolioId = portfolioId,
            CompanyId = companyId,
            TransactionType = "BUY",
            Quantity = 10,
            HypotheticalPrice = 120m
        };

        var actionResult = await controller.Simulate(request, default);
        var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(actionResult);
        var response = Assert.IsType<ShareSync.Application.Common.Models.ApiResponse<SimulationResultDto>>(okResult.Value);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(10, response.Data.SimulatedHoldingQuantity);
        Assert.Equal(120m, response.Data.SimulatedAverageCost);
    }

    [Fact]
    public async Task SimulatorController_WhenUnauthenticated_ThrowsUnauthorizedException()
    {
        using var context = CreateInMemoryDbContext();
        var service = new SimulatorService(context);
        var fakeUser = new FakeCurrentUserService(userId: null);
        var controller = new ShareSync.Web.Controllers.SimulatorController(service, fakeUser);

        var request = new SimulateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 10,
            HypotheticalPrice = 100m
        };

        await Assert.ThrowsAsync<UnauthorizedException>(() => controller.Simulate(request, default));
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(0.5)]
    public async Task Simulate_FractionalQuantity_ThrowsBadRequest(decimal fractionalQty)
    {
        using var context = CreateInMemoryDbContext();
        var service = new SimulatorService(context);

        var request = new SimulateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = fractionalQty,
            HypotheticalPrice = 100m
        };

        var ex = await Assert.ThrowsAsync<AppException>(() => service.SimulateTransactionAsync(request, userId: 1));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("Fractional shares are not supported", ex.Message);
    }

    private class FakeCurrentUserService : ShareSync.Application.Common.Interfaces.ICurrentUserService
    {
        public FakeCurrentUserService(int? userId = 1, string role = "INVESTOR")
        {
            UserId = userId;
            Role = role;
            Email = "investor@sharesync.com";
            IsAuthenticated = userId.HasValue;
        }

        public int? UserId { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public bool IsAuthenticated { get; set; }
    }
}
