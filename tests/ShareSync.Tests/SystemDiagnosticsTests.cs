using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Admin;
using ShareSync.Application.DTOs.Companies;
using ShareSync.Application.Interfaces;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using ShareSync.Web.Controllers;
using Xunit;

namespace ShareSync.Tests;

public class SystemDiagnosticsTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ShareSyncDbContext(options);
    }

    private class FakeDsePriceService : IDsePriceService
    {
        public Task<decimal?> FetchLatestPriceAsync(string tickerSymbol, CancellationToken cancellationToken = default)
            => Task.FromResult<decimal?>(100.00m);

        public Task<DseQuoteData?> FetchQuoteDetailsAsync(string tickerSymbol, CancellationToken cancellationToken = default)
            => Task.FromResult<DseQuoteData?>(null);

        public Task<ApiResponse<DsePriceUpdateResultDto>> SyncAllCompanyPricesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ApiResponse<DsePriceUpdateResultDto>.Ok(new DsePriceUpdateResultDto()));

        public Task<List<DseListedCompanyDto>> GetDseListedCompaniesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<DseListedCompanyDto>());

        public Task<CompanyDto> ImportCompanyFromDseAsync(string symbol, string? preferredName = null, string? preferredSector = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new CompanyDto { CompanyId = 999, TickerSymbol = symbol });
    }

    private class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(int? userId = null, string role = "INVESTOR")
        {
            UserId = userId;
            Role = role;
            Email = userId.HasValue ? $"{role.ToLower()}@sharesync.com" : null;
            IsAuthenticated = userId.HasValue;
        }

        public int? UserId { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public bool IsAuthenticated { get; set; }
    }

    // =========================================================================
    // 1. ADMIN CAN ACCESS
    // =========================================================================
    [Fact]
    public async Task Test1_Admin_CanAccessDiagnostics_Returns200Ok()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var fakeDse = new FakeDsePriceService();
        var adminService = new AdminService(context, fakeDse);
        var currentUserService = new FakeCurrentUserService(userId: 1, role: "ADMIN");
        var controller = new AdminController(adminService, fakeDse, currentUserService);

        // Act
        var result = await controller.GetSystemDiagnostics(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var apiResponse = Assert.IsType<ApiResponse<SystemDiagnosticsDto>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal("Connected", apiResponse.Data.DatabaseStatus);
        Assert.Equal("Healthy", apiResponse.Data.ApplicationStatus);
    }

    // =========================================================================
    // 2. NORMAL INVESTOR RECEIVES 403
    // =========================================================================
    [Fact]
    public async Task Test2_NormalInvestor_Receives403Forbidden()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var fakeDse = new FakeDsePriceService();
        var adminService = new AdminService(context, fakeDse);
        var investorUser = new FakeCurrentUserService(userId: 5, role: "INVESTOR");
        var controller = new AdminController(adminService, fakeDse, investorUser);

        // Act
        var result = await controller.GetSystemDiagnostics(CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);

        var apiResponse = Assert.IsType<ApiResponse>(objectResult.Value);
        Assert.False(apiResponse.Success);
        Assert.Contains("Access Denied", apiResponse.Message);
    }

    // =========================================================================
    // 3. UNAUTHENTICATED USER RECEIVES 401
    // =========================================================================
    [Fact]
    public async Task Test3_UnauthenticatedUser_Receives401Unauthorized()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var fakeDse = new FakeDsePriceService();
        var adminService = new AdminService(context, fakeDse);
        var unauthenticatedUser = new FakeCurrentUserService(userId: null, role: "ANONYMOUS");
        var controller = new AdminController(adminService, fakeDse, unauthenticatedUser);

        // Act
        var result = await controller.GetSystemDiagnostics(CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);

        var apiResponse = Assert.IsType<ApiResponse>(unauthorizedResult.Value);
        Assert.False(apiResponse.Success);
        Assert.Contains("Authentication required", apiResponse.Message);
    }

    // =========================================================================
    // 4. DATABASE HEALTH WORKS
    // =========================================================================
    [Fact]
    public async Task Test4_DatabaseHealth_ReportsConnectedAndHealthy()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var fakeDse = new FakeDsePriceService();
        var adminService = new AdminService(context, fakeDse);

        // Act
        var response = await adminService.GetSystemDiagnosticsAsync(CancellationToken.None);

        // Assert
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.True(response.Data.IsDatabaseConnected);
        Assert.Equal("Connected", response.Data.DatabaseStatus);
        Assert.Equal("Healthy", response.Data.ApplicationStatus);
        Assert.Equal("Running", response.Data.DseSyncStatus);
    }

    // =========================================================================
    // 5. STATISTICS ARE ACCURATE (EFFICIENT COUNT AGGREGATES)
    // =========================================================================
    [Fact]
    public async Task Test5_Statistics_AreAccurate_UsingCountAggregates()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var fakeDse = new FakeDsePriceService();
        var adminService = new AdminService(context, fakeDse);

        var syncDate = DateTime.UtcNow.AddMinutes(-15);

        // Seed Users
        context.Users.AddRange(
            new AppUser { UserId = 1, Name = "Admin User", Email = "admin@test.com", Role = "ADMIN" },
            new AppUser { UserId = 2, Name = "Investor One", Email = "inv1@test.com", Role = "INVESTOR" },
            new AppUser { UserId = 3, Name = "Investor Two", Email = "inv2@test.com", Role = "INVESTOR" }
        );

        // Seed Sectors & Companies
        var sector = new Sector { SectorId = 1, SectorName = "Banking" };
        context.Sectors.Add(sector);

        var comp1 = new Company { CompanyId = 1, TickerSymbol = "BANK1", CompanyName = "Bank One", SectorId = 1 };
        var comp2 = new Company { CompanyId = 2, TickerSymbol = "BANK2", CompanyName = "Bank Two", SectorId = 1 };
        context.Companies.AddRange(comp1, comp2);

        // Seed Portfolios
        var p1 = new Portfolio { PortfolioId = 1, UserId = 2, PortfolioName = "P1" };
        var p2 = new Portfolio { PortfolioId = 2, UserId = 3, PortfolioName = "P2" };
        context.Portfolios.AddRange(p1, p2);

        // Seed Transactions
        context.Transactions.AddRange(
            new Transaction { TransactionId = 1, PortfolioId = 1, CompanyId = 1, TransactionType = "BUY", Quantity = 10, PricePerShare = 50m },
            new Transaction { TransactionId = 2, PortfolioId = 2, CompanyId = 2, TransactionType = "BUY", Quantity = 20, PricePerShare = 100m }
        );

        // Seed Audits
        context.TransactionAudits.AddRange(
            new TransactionAudit { AuditId = 1, TransactionId = 1, ActionType = "INSERT", ActionDate = DateTime.UtcNow, ChangedBy = "SYSTEM" },
            new TransactionAudit { AuditId = 2, TransactionId = 2, ActionType = "INSERT", ActionDate = DateTime.UtcNow, ChangedBy = "SYSTEM" }
        );

        // Seed Price History (Last Sync)
        context.CompanyPriceHistories.Add(
            new CompanyPriceHistory { PriceHistoryId = 1, CompanyId = 1, RecordedAt = syncDate, Price = 52.50m }
        );

        await context.SaveChangesAsync();

        // Act
        var response = await adminService.GetSystemDiagnosticsAsync(CancellationToken.None);

        // Assert
        Assert.True(response.Success);
        var stats = response.Data;
        Assert.NotNull(stats);

        Assert.Equal(3, stats.UsersCount);
        Assert.Equal(2, stats.CompaniesCount);
        Assert.Equal(2, stats.PortfoliosCount);
        Assert.Equal(2, stats.TransactionsCount);
        Assert.Equal(2, stats.AuditRecordsCount);
        Assert.Equal(syncDate, stats.LastSyncTime);
        Assert.Equal("Successful", stats.SyncStatus);
    }

    // =========================================================================
    // 6. NO SECRETS ARE EXPOSED
    // =========================================================================
    [Fact]
    public async Task Test6_DiagnosticsPayload_DoesNotExposeSecretsOrSensitiveInfo()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var fakeDse = new FakeDsePriceService();
        var adminService = new AdminService(context, fakeDse);

        // Act
        var response = await adminService.GetSystemDiagnosticsAsync(CancellationToken.None);
        var json = JsonSerializer.Serialize(response.Data);
        var lowerJson = json.ToLowerInvariant();

        // Assert: Ensure no sensitive keywords, passwords, tokens, or server filesystem paths are present
        Assert.DoesNotContain("password", lowerJson);
        Assert.DoesNotContain("secret", lowerJson);
        Assert.DoesNotContain("connectionstring", lowerJson);
        Assert.DoesNotContain("datasource", lowerJson);
        Assert.DoesNotContain("user id=", lowerJson);
        Assert.DoesNotContain("jwt", lowerJson);
        Assert.DoesNotContain("c:\\", lowerJson);
        Assert.DoesNotContain("/etc/", lowerJson);
        Assert.DoesNotContain("appsettings", lowerJson);
    }
}
