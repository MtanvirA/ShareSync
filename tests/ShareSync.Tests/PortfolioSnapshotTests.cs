using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.DTOs.Snapshots;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class PortfolioSnapshotTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new ShareSyncDbContext(options);

        // Seed Users
        context.Users.AddRange(
            new AppUser { UserId = 1, Email = "user1@sharesync.com", Name = "User One", PasswordHash = "hash1" },
            new AppUser { UserId = 2, Email = "user2@sharesync.com", Name = "User Two", PasswordHash = "hash2" }
        );

        // Seed Portfolios
        context.Portfolios.AddRange(
            new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "User 1 Portfolio" },
            new Portfolio { PortfolioId = 2, UserId = 2, PortfolioName = "User 2 Portfolio" }
        );

        // Seed Companies
        context.Companies.AddRange(
            new Company { CompanyId = 1, TickerSymbol = "BX", CompanyName = "Beximco", CurrentPrice = 120.00m, SectorId = 1 },
            new Company { CompanyId = 2, TickerSymbol = "GP", CompanyName = "Grameenphone", CurrentPrice = 300.00m, SectorId = 1 }
        );

        // Seed Transactions for Portfolio 1:
        // 100 shares of BX (100 * 120 = 12,000)
        // 50 shares of GP (50 * 300 = 15,000)
        // Total holdings market value = 27,000
        context.Transactions.AddRange(
            new Transaction
            {
                TransactionId = 1,
                PortfolioId = 1,
                CompanyId = 1,
                TransactionType = "BUY",
                Quantity = 100,
                PricePerShare = 110m,
                TransactionDate = new DateTime(2026, 1, 10)
            },
            new Transaction
            {
                TransactionId = 2,
                PortfolioId = 1,
                CompanyId = 2,
                TransactionType = "BUY",
                Quantity = 50,
                PricePerShare = 280m,
                TransactionDate = new DateTime(2026, 1, 15)
            }
        );

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task CreateSnapshot_WithExplicitValue_SavesSuccessfully()
    {
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioSnapshotService(context);

        var request = new CreateSnapshotRequestDto
        {
            SnapshotDate = new DateTime(2026, 9, 1),
            TotalValue = 100000.00m
        };

        var response = await service.CreateSnapshotAsync(1, request, userId: 1);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(100000.00m, response.Data.TotalValue);
        Assert.Equal(1, response.Data.PortfolioId);
        Assert.Equal(new DateTime(2026, 9, 1), response.Data.SnapshotDate);
    }

    [Fact]
    public async Task CreateSnapshot_WithoutExplicitValue_AutoCalculatesFromHoldings()
    {
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioSnapshotService(context);

        var request = new CreateSnapshotRequestDto
        {
            SnapshotDate = new DateTime(2026, 2, 1),
            TotalValue = null // auto-calculate
        };

        var response = await service.CreateSnapshotAsync(1, request, userId: 1);

        Assert.True(response.Success);
        // Portfolio 1 holdings: 100 * 120 + 50 * 300 = 12,000 + 15,000 = 27,000
        Assert.Equal(27000.00m, response.Data.TotalValue);
    }

    [Fact]
    public async Task CreateSnapshot_WithNegativeValue_ThrowsValidationException()
    {
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioSnapshotService(context);

        var request = new CreateSnapshotRequestDto
        {
            SnapshotDate = new DateTime(2026, 9, 1),
            TotalValue = -500.00m
        };

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateSnapshotAsync(1, request, userId: 1));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("Total value cannot be negative", ex.Message);
    }

    [Fact]
    public async Task CreateSnapshot_WithNonExistentPortfolio_ThrowsNotFound()
    {
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioSnapshotService(context);

        var request = new CreateSnapshotRequestDto
        {
            SnapshotDate = new DateTime(2026, 9, 1),
            TotalValue = 50000m
        };

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateSnapshotAsync(999, request, userId: 1));
    }

    [Fact]
    public async Task CreateSnapshot_WithUnauthorizedPortfolio_ThrowsForbidden()
    {
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioSnapshotService(context);

        var request = new CreateSnapshotRequestDto
        {
            SnapshotDate = new DateTime(2026, 9, 1),
            TotalValue = 50000m
        };

        // User 2 trying to create snapshot for User 1's portfolio (id=1)
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateSnapshotAsync(1, request, userId: 2));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task CreateSnapshot_DuplicateDateForSamePortfolio_ThrowsConflict()
    {
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioSnapshotService(context);

        var request = new CreateSnapshotRequestDto
        {
            SnapshotDate = new DateTime(2026, 9, 1),
            TotalValue = 50000m
        };

        await service.CreateSnapshotAsync(1, request, userId: 1);

        // Attempt duplicate snapshot on same date
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateSnapshotAsync(1, request, userId: 1));

        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task GetPortfolioSnapshots_ReturnsChronologicalOrderAndChangeMetrics()
    {
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioSnapshotService(context);

        // Add 3 snapshots out of order
        await service.CreateSnapshotAsync(1, new CreateSnapshotRequestDto { SnapshotDate = new DateTime(2026, 9, 3), TotalValue = 120000m }, userId: 1);
        await service.CreateSnapshotAsync(1, new CreateSnapshotRequestDto { SnapshotDate = new DateTime(2026, 9, 1), TotalValue = 100000m }, userId: 1);
        await service.CreateSnapshotAsync(1, new CreateSnapshotRequestDto { SnapshotDate = new DateTime(2026, 9, 2), TotalValue = 110000m }, userId: 1);

        var response = await service.GetPortfolioSnapshotsAsync(1, userId: 1);

        Assert.True(response.Success);
        var list = response.Data;
        Assert.Equal(3, list.Count);

        // Chronological order verification
        Assert.Equal(new DateTime(2026, 9, 1), list[0].SnapshotDate);
        Assert.Null(list[0].ValueChange); // first has no previous

        Assert.Equal(new DateTime(2026, 9, 2), list[1].SnapshotDate);
        Assert.Equal(10000m, list[1].ValueChange); // 110k - 100k
        Assert.Equal(10.00m, list[1].PercentageChange); // +10%

        Assert.Equal(new DateTime(2026, 9, 3), list[2].SnapshotDate);
        Assert.Equal(10000m, list[2].ValueChange); // 120k - 110k
        Assert.Equal(9.09m, list[2].PercentageChange); // +9.09%
    }

    [Fact]
    public async Task GetPortfolioSnapshots_WithDateRangeFilter_ReturnsFilteredSubset()
    {
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioSnapshotService(context);

        await service.CreateSnapshotAsync(1, new CreateSnapshotRequestDto { SnapshotDate = new DateTime(2026, 8, 1), TotalValue = 80000m }, userId: 1);
        await service.CreateSnapshotAsync(1, new CreateSnapshotRequestDto { SnapshotDate = new DateTime(2026, 8, 15), TotalValue = 85000m }, userId: 1);
        await service.CreateSnapshotAsync(1, new CreateSnapshotRequestDto { SnapshotDate = new DateTime(2026, 9, 1), TotalValue = 90000m }, userId: 1);

        var filter = new SnapshotFilterDto
        {
            StartDate = new DateTime(2026, 8, 10),
            EndDate = new DateTime(2026, 8, 20)
        };

        var response = await service.GetPortfolioSnapshotsAsync(1, userId: 1, filter);

        Assert.True(response.Success);
        Assert.Single(response.Data);
        Assert.Equal(new DateTime(2026, 8, 15), response.Data[0].SnapshotDate);
    }

    [Fact]
    public async Task GetSnapshotById_Existing_ReturnsDto()
    {
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioSnapshotService(context);

        var created = await service.CreateSnapshotAsync(1, new CreateSnapshotRequestDto { SnapshotDate = new DateTime(2026, 9, 1), TotalValue = 75000m }, userId: 1);

        var response = await service.GetSnapshotByIdAsync(created.Data.SnapshotId, userId: 1);

        Assert.True(response.Success);
        Assert.Equal(75000m, response.Data.TotalValue);
        Assert.Equal("User 1 Portfolio", response.Data.PortfolioName);
    }

    [Fact]
    public async Task GetSnapshotById_UnauthorizedUser_ThrowsForbidden()
    {
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioSnapshotService(context);

        var created = await service.CreateSnapshotAsync(1, new CreateSnapshotRequestDto { SnapshotDate = new DateTime(2026, 9, 1), TotalValue = 75000m }, userId: 1);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.GetSnapshotByIdAsync(created.Data.SnapshotId, userId: 2));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task DeleteSnapshot_Existing_DeletesSuccessfully()
    {
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioSnapshotService(context);

        var created = await service.CreateSnapshotAsync(1, new CreateSnapshotRequestDto { SnapshotDate = new DateTime(2026, 9, 1), TotalValue = 75000m }, userId: 1);

        var deleteRes = await service.DeleteSnapshotAsync(created.Data.SnapshotId, userId: 1);
        Assert.True(deleteRes.Success);

        // Verify it is gone
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetSnapshotByIdAsync(created.Data.SnapshotId, userId: 1));
    }

    [Fact]
    public async Task DeleteSnapshot_UnauthorizedUser_ThrowsForbidden()
    {
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioSnapshotService(context);

        var created = await service.CreateSnapshotAsync(1, new CreateSnapshotRequestDto { SnapshotDate = new DateTime(2026, 9, 1), TotalValue = 75000m }, userId: 1);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.DeleteSnapshotAsync(created.Data.SnapshotId, userId: 2));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task GetPerformanceData_CalculatesStartingCurrentAndOverallChange()
    {
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioSnapshotService(context);

        await service.CreateSnapshotAsync(1, new CreateSnapshotRequestDto { SnapshotDate = new DateTime(2026, 9, 1), TotalValue = 100000m }, userId: 1);
        await service.CreateSnapshotAsync(1, new CreateSnapshotRequestDto { SnapshotDate = new DateTime(2026, 9, 10), TotalValue = 120000m }, userId: 1);

        var response = await service.GetPerformanceDataAsync(1, userId: 1);

        Assert.True(response.Success);
        var data = response.Data;
        Assert.Equal(2, data.Labels.Count);
        Assert.Equal(100000m, data.StartingValue);
        Assert.Equal(120000m, data.CurrentValue);
        Assert.Equal(20000m, data.OverallChange);
        Assert.Equal(20.00m, data.OverallChangePercentage);
    }

    [Fact]
    public async Task GetPerformanceData_WhenNoSnapshots_ReturnsCurrentHoldingsBaseline()
    {
        using var context = CreateInMemoryDbContext();
        var service = new PortfolioSnapshotService(context);

        // No snapshots created yet
        var response = await service.GetPerformanceDataAsync(1, userId: 1);

        Assert.True(response.Success);
        var data = response.Data;
        Assert.Single(data.Labels);
        // Auto-computed holdings value = 27,000
        Assert.Equal(27000m, data.CurrentValue);
        Assert.Equal(27000m, data.StartingValue);
        Assert.Equal(0m, data.OverallChange);
    }
}
