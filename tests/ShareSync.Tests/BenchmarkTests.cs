using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Benchmarks;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using ShareSync.Web.Controllers;
using Xunit;

namespace ShareSync.Tests;

public class BenchmarkTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new ShareSyncDbContext(options);

        // Seed Users
        context.Users.AddRange(
            new AppUser { UserId = 1, Email = "investor1@sharesync.com", Name = "Investor One", PasswordHash = "hash1" },
            new AppUser { UserId = 2, Email = "investor2@sharesync.com", Name = "Investor Two", PasswordHash = "hash2" }
        );

        // Seed Portfolios
        context.Portfolios.AddRange(
            new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "Growth Fund" },
            new Portfolio { PortfolioId = 2, UserId = 1, PortfolioName = "Dividend Fund" },
            new Portfolio { PortfolioId = 3, UserId = 1, PortfolioName = "New Empty Fund" },
            new Portfolio { PortfolioId = 4, UserId = 2, PortfolioName = "User 2 Fund" }
        );

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task ComparePortfolioAsync_WithKnownPortfolioAndBenchmark_CalculatesCorrectReturnsAndDifference()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var dataProvider = new BenchmarkDataProvider();
        var service = new BenchmarkService(context, dataProvider);

        // Portfolio 1: 100,000 on 2026-01-01 -> 118,400 on 2026-10-01 (+18.4%)
        // Benchmark (DSEX): 6,245.00 on 2026-01-01 -> 7,131.79 on 2026-10-01 (+14.2%)
        // Expected Outperformance: 18.4% - 14.2% = +4.2%
        context.PortfolioSnapshots.AddRange(
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = new DateTime(2026, 01, 01), TotalValue = 100000m },
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = new DateTime(2026, 10, 01), TotalValue = 118400m }
        );
        await context.SaveChangesAsync();

        var request = new BenchmarkComparisonRequestDto
        {
            PortfolioId = 1,
            BenchmarkCode = "DSEX",
            Period = "ALL"
        };

        // Act
        var result = await service.ComparePortfolioAsync(request, userId: 1);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.HasSufficientData);
        Assert.Equal("Growth Fund", result.PortfolioName);
        Assert.Equal("DSEX", result.BenchmarkCode);
        Assert.Equal(100000m, result.PortfolioStartingValue);
        Assert.Equal(118400m, result.PortfolioEndingValue);
        Assert.Equal(18.40m, result.PortfolioReturnPercentage);

        Assert.Equal(6245.00m, result.BenchmarkStartingValue);
        Assert.Equal(7131.79m, result.BenchmarkEndingValue);
        Assert.Equal(14.20m, result.BenchmarkReturnPercentage);

        Assert.Equal(4.20m, result.OutperformancePercentage);
        Assert.True(result.IsOutperforming);
        Assert.Equal(2, result.Labels.Count);
        Assert.Equal(0.00m, result.PortfolioReturns[0]);
        Assert.Equal(18.40m, result.PortfolioReturns[1]);
        Assert.Equal(0.00m, result.BenchmarkReturns[0]);
        Assert.Equal(14.20m, result.BenchmarkReturns[1]);
    }

    [Fact]
    public async Task ComparePortfolioAsync_DateRangeHandling_RespectsPeriodFilters()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var dataProvider = new BenchmarkDataProvider();
        var service = new BenchmarkService(context, dataProvider);

        var now = DateTime.UtcNow.Date;
        context.PortfolioSnapshots.AddRange(
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = now.AddYears(-1), TotalValue = 80000m },
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = now.AddMonths(-6), TotalValue = 90000m },
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = now.AddMonths(-3), TotalValue = 100000m },
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = now.AddMonths(-1), TotalValue = 110000m },
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = now, TotalValue = 120000m }
        );
        await context.SaveChangesAsync();

        // 1. Compare 1M
        var req1M = new BenchmarkComparisonRequestDto { PortfolioId = 1, BenchmarkCode = "DSEX", Period = "1M" };
        var res1M = await service.ComparePortfolioAsync(req1M, userId: 1);
        Assert.True(res1M.HasSufficientData);
        Assert.Equal(110000m, res1M.PortfolioStartingValue);
        Assert.Equal(120000m, res1M.PortfolioEndingValue);
        Assert.Equal(2, res1M.Labels.Count);

        // 2. Compare 3M
        var req3M = new BenchmarkComparisonRequestDto { PortfolioId = 1, BenchmarkCode = "DSEX", Period = "3M" };
        var res3M = await service.ComparePortfolioAsync(req3M, userId: 1);
        Assert.True(res3M.HasSufficientData);
        Assert.Equal(100000m, res3M.PortfolioStartingValue);
        Assert.Equal(120000m, res3M.PortfolioEndingValue);

        // 3. Compare ALL
        var reqAll = new BenchmarkComparisonRequestDto { PortfolioId = 1, BenchmarkCode = "DSEX", Period = "ALL" };
        var resAll = await service.ComparePortfolioAsync(reqAll, userId: 1);
        Assert.True(resAll.HasSufficientData);
        Assert.Equal(80000m, resAll.PortfolioStartingValue);
        Assert.Equal(120000m, resAll.PortfolioEndingValue);
        Assert.Equal(50.00m, resAll.PortfolioReturnPercentage);
    }

    [Fact]
    public async Task ComparePortfolioAsync_WithInsufficientData_ReturnsInsufficientDataFlagAndAvailablePeriods()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var dataProvider = new BenchmarkDataProvider();
        var service = new BenchmarkService(context, dataProvider);

        // Only 1 snapshot exists (cannot compute return without interpolating unverified data)
        context.PortfolioSnapshots.Add(
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = DateTime.UtcNow.Date, TotalValue = 100000m }
        );
        await context.SaveChangesAsync();

        var request = new BenchmarkComparisonRequestDto { PortfolioId = 1, Period = "ALL" };

        // Act
        var result = await service.ComparePortfolioAsync(request, userId: 1);

        // Assert
        Assert.False(result.HasSufficientData);
        Assert.NotNull(result.Message);
        Assert.Contains("Insufficient", result.Message);
        Assert.Empty(result.AvailablePeriods);

        // Now add a second snapshot only 10 days older (1Y requested)
        context.PortfolioSnapshots.Add(
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = DateTime.UtcNow.Date.AddDays(-10), TotalValue = 98000m }
        );
        await context.SaveChangesAsync();

        var req1Y = new BenchmarkComparisonRequestDto { PortfolioId = 1, Period = "1Y" };
        var res1Y = await service.ComparePortfolioAsync(req1Y, userId: 1);

        Assert.False(res1Y.HasSufficientData);
        Assert.Contains("Insufficient", res1Y.Message);
        Assert.DoesNotContain("1Y", res1Y.AvailablePeriods);
    }

    [Fact]
    public async Task ComparePortfolioAsync_WithOtherUsersPortfolio_ThrowsForbidden()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var dataProvider = new BenchmarkDataProvider();
        var service = new BenchmarkService(context, dataProvider);

        // Portfolio 4 belongs to User 2. User 1 tries to access it.
        var request = new BenchmarkComparisonRequestDto { PortfolioId = 4, Period = "ALL" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppException>(() => service.ComparePortfolioAsync(request, userId: 1));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ComparePortfolioAsync_WithNonExistentPortfolio_ThrowsNotFound()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var dataProvider = new BenchmarkDataProvider();
        var service = new BenchmarkService(context, dataProvider);

        var request = new BenchmarkComparisonRequestDto { PortfolioId = 9999, Period = "ALL" };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.ComparePortfolioAsync(request, userId: 1));
    }

    [Fact]
    public async Task BenchmarksController_GetAvailableBenchmarks_ReturnsList()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var dataProvider = new BenchmarkDataProvider();
        var service = new BenchmarkService(context, dataProvider);

        var fakeUser = new FakeCurrentUserService(1);
        var controller = new BenchmarksController(service, fakeUser);

        // Act
        var actionResult = await controller.GetBenchmarks(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var apiResponse = Assert.IsType<ApiResponse<List<BenchmarkDto>>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.NotEmpty(apiResponse.Data!);
        Assert.Contains(apiResponse.Data!, b => b.BenchmarkCode == "DSEX");
        Assert.Contains(apiResponse.Data!, b => b.BenchmarkCode == "DS30");
    }

    [Fact]
    public async Task BenchmarksController_Compare_ReturnsOkWithBenchmarkComparisonDto()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var dataProvider = new BenchmarkDataProvider();
        var service = new BenchmarkService(context, dataProvider);

        context.PortfolioSnapshots.AddRange(
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = new DateTime(2026, 01, 01), TotalValue = 100000m },
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = new DateTime(2026, 10, 01), TotalValue = 115000m }
        );
        await context.SaveChangesAsync();

        var fakeUser = new FakeCurrentUserService(1);
        var controller = new BenchmarksController(service, fakeUser);

        var request = new BenchmarkComparisonRequestDto { PortfolioId = 1, BenchmarkCode = "DSEX", Period = "ALL" };

        // Act
        var actionResult = await controller.Compare(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var apiResponse = Assert.IsType<ApiResponse<BenchmarkComparisonDto>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.True(apiResponse.Data!.HasSufficientData);
        Assert.Equal(15.00m, apiResponse.Data.PortfolioReturnPercentage);
    }

    private class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(int? userId)
        {
            UserId = userId;
        }

        public int? UserId { get; }
        public string? Email => "test@sharesync.com";
        public string? Role => "INVESTOR";
        public bool IsAuthenticated => UserId.HasValue;
    }
}
