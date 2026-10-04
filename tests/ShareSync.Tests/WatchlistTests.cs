using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.DTOs.Watchlists;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class WatchlistTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new ShareSyncDbContext(options);

        // Seed companies for test isolation
        context.Companies.AddRange(
            new Company { CompanyId = 1, TickerSymbol = "BX", CompanyName = "Beximco Limited", CurrentPrice = 120.00m, SectorId = 1 },
            new Company { CompanyId = 2, TickerSymbol = "GP", CompanyName = "Grameenphone Ltd.", CurrentPrice = 295.00m, SectorId = 1 },
            new Company { CompanyId = 3, TickerSymbol = "BATBC", CompanyName = "BAT Bangladesh", CurrentPrice = 450.00m, SectorId = 1 },
            new Company { CompanyId = 4, TickerSymbol = "SQURPHARMA", CompanyName = "Square Pharmaceuticals", CurrentPrice = 235.50m, SectorId = 1 }
        );
        context.SaveChanges();

        return context;
    }

    [Fact]
    public async Task CreateWatchlist_WithValidData_ReturnsSuccessAndPersists()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);
        const int userId = 1;

        var request = new CreateWatchlistRequestDto
        {
            WatchlistName = "Blue Chips",
            Description = "Top market cap dividend stocks"
        };

        var response = await service.CreateWatchlistAsync(request, userId);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("Blue Chips", response.Data.WatchlistName);
        Assert.Equal("Top market cap dividend stocks", response.Data.Description);

        var saved = await context.Watchlists.FirstOrDefaultAsync(w => w.WatchlistName == "Blue Chips");
        Assert.NotNull(saved);
        Assert.Equal(userId, saved.UserId);
    }

    [Fact]
    public async Task CreateWatchlist_WithDuplicateNameForSameUser_ThrowsConflict()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);
        const int userId = 1;

        await service.CreateWatchlistAsync(new CreateWatchlistRequestDto
        {
            WatchlistName = "Tech Stocks"
        }, userId);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateWatchlistAsync(new CreateWatchlistRequestDto
            {
                WatchlistName = "Tech Stocks"
            }, userId));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task CreateWatchlist_WithSameNameForDifferentUser_Succeeds()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var r1 = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "Growth" }, userId: 1);
        var r2 = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "Growth" }, userId: 2);

        Assert.True(r1.Success);
        Assert.True(r2.Success);
        Assert.NotEqual(r1.Data!.WatchlistId, r2.Data!.WatchlistId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateWatchlist_WithEmptyName_ThrowsBadRequest(string? name)
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = name! }, userId: 1));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task GetUserWatchlists_ReturnsOnlyBelongingToUser()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "User 1 List A" }, userId: 1);
        await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "User 1 List B" }, userId: 1);
        await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "User 2 List A" }, userId: 2);

        var user1Lists = await service.GetUserWatchlistsAsync(userId: 1);
        var user2Lists = await service.GetUserWatchlistsAsync(userId: 2);

        Assert.Equal(2, user1Lists.Data!.Count);
        Assert.Single(user2Lists.Data!);
        Assert.All(user1Lists.Data, w => Assert.Equal(1, w.UserId));
    }

    [Fact]
    public async Task GetWatchlistById_WithOwner_ReturnsFullDetailWithMetrics()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var created = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "Favorites" }, userId: 1);
        int watchlistId = created.Data!.WatchlistId;

        // Add company 1 (BX price 120, target 130 -> not reached, distance ~8.33%)
        await service.AddItemAsync(watchlistId, new AddWatchlistItemRequestDto { CompanyId = 1, TargetPrice = 130.00m }, userId: 1);

        // Add company 2 (GP price 295, target 280 -> reached)
        await service.AddItemAsync(watchlistId, new AddWatchlistItemRequestDto { CompanyId = 2, TargetPrice = 280.00m }, userId: 1);

        var detail = await service.GetWatchlistByIdAsync(watchlistId, userId: 1);

        Assert.True(detail.Success);
        Assert.Equal(2, detail.Data!.TotalWatching);
        Assert.Equal(1, detail.Data.AboveTargetCount);
        Assert.Equal(2, detail.Data.Items.Count);
    }

    [Fact]
    public async Task GetWatchlistById_WithDifferentUser_ThrowsForbidden()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var created = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "Secret List" }, userId: 1);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.GetWatchlistByIdAsync(created.Data!.WatchlistId, userId: 2));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task GetWatchlistById_WithInvalidId_ThrowsNotFound()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetWatchlistByIdAsync(9999, userId: 1));
    }

    [Fact]
    public async Task UpdateWatchlist_WithOwner_UpdatesSuccessfully()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var created = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "Old Name" }, userId: 1);

        var updateReq = new UpdateWatchlistRequestDto
        {
            WatchlistName = "New Name",
            Description = "Updated description"
        };

        var updated = await service.UpdateWatchlistAsync(created.Data!.WatchlistId, updateReq, userId: 1);

        Assert.True(updated.Success);
        Assert.Equal("New Name", updated.Data!.WatchlistName);
        Assert.Equal("Updated description", updated.Data.Description);
    }

    [Fact]
    public async Task UpdateWatchlist_WithDifferentUser_ThrowsForbidden()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var created = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "User 1 List" }, userId: 1);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.UpdateWatchlistAsync(created.Data!.WatchlistId, new UpdateWatchlistRequestDto { WatchlistName = "Hacked Name" }, userId: 2));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateWatchlist_WithDuplicateName_ThrowsConflict()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "List A" }, userId: 1);
        var listB = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "List B" }, userId: 1);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.UpdateWatchlistAsync(listB.Data!.WatchlistId, new UpdateWatchlistRequestDto { WatchlistName = "List A" }, userId: 1));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task DeleteWatchlist_WithOwner_DeletesWatchlistAndCascadeItems()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var created = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "To Delete" }, userId: 1);
        int watchlistId = created.Data!.WatchlistId;

        await service.AddItemAsync(watchlistId, new AddWatchlistItemRequestDto { CompanyId = 1 }, userId: 1);

        var deleteRes = await service.DeleteWatchlistAsync(watchlistId, userId: 1);
        Assert.True(deleteRes.Success);

        var inDb = await context.Watchlists.FindAsync(watchlistId);
        Assert.Null(inDb);

        var itemsInDb = await context.WatchlistItems.Where(wi => wi.WatchlistId == watchlistId).ToListAsync();
        Assert.Empty(itemsInDb);
    }

    [Fact]
    public async Task DeleteWatchlist_WithDifferentUser_ThrowsForbidden()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var created = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "Protected List" }, userId: 1);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.DeleteWatchlistAsync(created.Data!.WatchlistId, userId: 2));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task AddItem_WithValidCompanyAndTargetPrice_ReturnsSuccess()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var created = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "My Stocks" }, userId: 1);
        int watchlistId = created.Data!.WatchlistId;

        var addRes = await service.AddItemAsync(watchlistId, new AddWatchlistItemRequestDto
        {
            CompanyId = 1,
            TargetPrice = 140.00m
        }, userId: 1);

        Assert.True(addRes.Success);
        Assert.Equal(1, addRes.Data!.CompanyId);
        Assert.Equal("BX", addRes.Data.TickerSymbol);
        Assert.Equal(140.00m, addRes.Data.TargetPrice);
    }

    [Fact]
    public async Task AddItem_WithDuplicateCompanyInSameWatchlist_ThrowsConflict()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var created = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "No Dups List" }, userId: 1);
        int watchlistId = created.Data!.WatchlistId;

        await service.AddItemAsync(watchlistId, new AddWatchlistItemRequestDto { CompanyId = 1 }, userId: 1);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.AddItemAsync(watchlistId, new AddWatchlistItemRequestDto { CompanyId = 1 }, userId: 1));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task AddItem_ToDifferentUserWatchlist_ThrowsForbidden()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var created = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "User 1 List" }, userId: 1);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.AddItemAsync(created.Data!.WatchlistId, new AddWatchlistItemRequestDto { CompanyId = 1 }, userId: 2));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task AddItem_WithNonexistentCompany_ThrowsNotFound()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var created = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "List" }, userId: 1);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.AddItemAsync(created.Data!.WatchlistId, new AddWatchlistItemRequestDto { CompanyId = 9999 }, userId: 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task AddItem_WithZeroOrNegativeTargetPrice_ThrowsBadRequest(decimal invalidPrice)
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var created = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "List" }, userId: 1);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.AddItemAsync(created.Data!.WatchlistId, new AddWatchlistItemRequestDto
            {
                CompanyId = 1,
                TargetPrice = invalidPrice
            }, userId: 1));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateItem_TargetPrice_UpdatesSuccessfully()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var created = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "List" }, userId: 1);
        int watchlistId = created.Data!.WatchlistId;

        await service.AddItemAsync(watchlistId, new AddWatchlistItemRequestDto { CompanyId = 1, TargetPrice = 130.00m }, userId: 1);

        var updateRes = await service.UpdateItemAsync(watchlistId, 1, new UpdateWatchlistItemRequestDto
        {
            TargetPrice = 145.00m
        }, userId: 1);

        Assert.True(updateRes.Success);
        Assert.Equal(145.00m, updateRes.Data!.TargetPrice);

        var itemInDb = await context.WatchlistItems.FindAsync(watchlistId, 1);
        Assert.Equal(145.00m, itemInDb!.TargetPrice);
    }

    [Fact]
    public async Task UpdateItem_WithDifferentUser_ThrowsForbidden()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var created = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "User 1 List" }, userId: 1);
        int watchlistId = created.Data!.WatchlistId;

        await service.AddItemAsync(watchlistId, new AddWatchlistItemRequestDto { CompanyId = 1 }, userId: 1);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.UpdateItemAsync(watchlistId, 1, new UpdateWatchlistItemRequestDto { TargetPrice = 200m }, userId: 2));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateItem_WithNonexistentItem_ThrowsNotFound()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var created = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "List" }, userId: 1);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateItemAsync(created.Data!.WatchlistId, 9999, new UpdateWatchlistItemRequestDto { TargetPrice = 100m }, userId: 1));
    }

    [Fact]
    public async Task UpdateItem_WithNegativeTargetPrice_ThrowsBadRequest()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var created = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "List" }, userId: 1);
        int watchlistId = created.Data!.WatchlistId;

        await service.AddItemAsync(watchlistId, new AddWatchlistItemRequestDto { CompanyId = 1 }, userId: 1);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.UpdateItemAsync(watchlistId, 1, new UpdateWatchlistItemRequestDto { TargetPrice = -20m }, userId: 1));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task RemoveItem_WithOwner_RemovesItemSuccessfully()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var created = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "List" }, userId: 1);
        int watchlistId = created.Data!.WatchlistId;

        await service.AddItemAsync(watchlistId, new AddWatchlistItemRequestDto { CompanyId = 1 }, userId: 1);

        var removeRes = await service.RemoveItemAsync(watchlistId, 1, userId: 1);
        Assert.True(removeRes.Success);

        var item = await context.WatchlistItems.FindAsync(watchlistId, 1);
        Assert.Null(item);
    }

    [Fact]
    public async Task RemoveItem_WithDifferentUser_ThrowsForbidden()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var created = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "List" }, userId: 1);
        int watchlistId = created.Data!.WatchlistId;

        await service.AddItemAsync(watchlistId, new AddWatchlistItemRequestDto { CompanyId = 1 }, userId: 1);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.RemoveItemAsync(watchlistId, 1, userId: 2));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task RemoveItem_NonexistentItem_ThrowsNotFound()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var created = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto { WatchlistName = "List" }, userId: 1);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.RemoveItemAsync(created.Data!.WatchlistId, 999, userId: 1));
    }

    [Fact]
    public async Task Watchlist_SqlInjectionPayloads_HandledSafely()
    {
        using var context = CreateInMemoryDbContext();
        var service = new WatchlistService(context);

        var maliciousName = "'; DROP TABLE WATCHLISTS; --";
        var maliciousDesc = "<script>alert('xss')</script>";

        var response = await service.CreateWatchlistAsync(new CreateWatchlistRequestDto
        {
            WatchlistName = maliciousName,
            Description = maliciousDesc
        }, userId: 1);

        Assert.True(response.Success);
        Assert.Equal(maliciousName, response.Data!.WatchlistName);
        Assert.Equal(maliciousDesc, response.Data.Description);

        var inDb = await context.Watchlists.FindAsync(response.Data.WatchlistId);
        Assert.NotNull(inDb);
        Assert.Equal(maliciousName, inDb.WatchlistName);
    }
}
