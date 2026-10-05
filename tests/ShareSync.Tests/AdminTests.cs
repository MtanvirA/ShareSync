using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Admin;
using ShareSync.Application.DTOs.Auth;
using ShareSync.Application.DTOs.Companies;
using ShareSync.Application.Interfaces;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using ShareSync.Infrastructure.Security;
using ShareSync.Web.Controllers;
using Xunit;

namespace ShareSync.Tests;

public class AdminTests
{
    private readonly PasswordHasher _passwordHasher = new();

    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ShareSyncDbContext(options);
    }

    private IConfiguration CreateTestConfiguration()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Jwt:Secret", "ShareSyncSuperSecretKeyForAcademicProjectSecurity2026#LongEnoughKey"},
            {"Jwt:Issuer", "ShareSyncServer"},
            {"Jwt:Audience", "ShareSyncClient"},
            {"Jwt:ExpiryDays", "7"}
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    private class FakeDsePriceService : IDsePriceService
    {
        public Task<decimal?> FetchLatestPriceAsync(string tickerSymbol, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<decimal?>(350.00m);
        }

        public Task<DseQuoteData?> FetchQuoteDetailsAsync(string tickerSymbol, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DseQuoteData?>(new DseQuoteData
            {
                Price = 350.00m,
                OpenPrice = 345.00m,
                HighPrice = 355.00m,
                LowPrice = 342.00m,
                Volume = 100000
            });
        }

        public Task<ApiResponse<DsePriceUpdateResultDto>> SyncAllCompanyPricesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ApiResponse<DsePriceUpdateResultDto>.Ok(new DsePriceUpdateResultDto
            {
                UpdatedCount = 8,
                HistoryCount = 8,
                SyncedAt = DateTime.UtcNow,
                UpdatedPrices = new Dictionary<string, decimal> { { "GP", 350m } }
            }, "Simulated DSE price sync completed."));
        }

        public Task<List<DseListedCompanyDto>> GetDseListedCompaniesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<DseListedCompanyDto>
            {
                new() { Symbol = "GP", Name = "Grameenphone Ltd.", Sector = "Telecommunication", Category = "A", MarketCapMn = 386000m, IsAdded = true, LocalCompanyId = 1, LocalPrice = 350.00m },
                new() { Symbol = "BATBC", Name = "British American Tobacco Bangladesh", Sector = "Food & Allied", Category = "A", MarketCapMn = 280000m, IsAdded = false }
            });
        }

        public Task<CompanyDto> ImportCompanyFromDseAsync(string symbol, string? preferredName = null, string? preferredSector = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CompanyDto
            {
                CompanyId = 999,
                TickerSymbol = symbol.ToUpperInvariant(),
                CompanyName = preferredName ?? $"{symbol} Limited",
                CurrentPrice = 300.00m,
                SectorName = preferredSector ?? "Miscellaneous"
            });
        }
    }

    private class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(int? userId = 1, string role = "ADMIN")
        {
            UserId = userId;
            Role = role;
            Email = "admin@sharesync.com";
            IsAuthenticated = userId.HasValue;
        }

        public int? UserId { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public bool IsAuthenticated { get; set; }
    }

    // =========================================================================
    // 1. AUTHORIZATION & ROLE ATTRIBUTE CHECKS
    // =========================================================================

    [Fact]
    public void AdminController_HasAuthorizeAttribute_WithAdminRole()
    {
        // Assert: Ensure AdminController is guarded by [Authorize(Roles = "ADMIN")]
        var authorizeAttribute = typeof(AdminController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .FirstOrDefault() as AuthorizeAttribute;

        Assert.NotNull(authorizeAttribute);
        Assert.Equal("ADMIN", authorizeAttribute.Roles);
    }

    [Fact]
    public async Task AdminLogin_WithAdminCredentials_GeneratesTokenWithAdminRole()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var tokenGenerator = new JwtTokenGenerator(config);
        var authService = new AuthService(context, _passwordHasher, tokenGenerator);

        var adminUser = new AppUser
        {
            UserId = 1,
            Name = "System Administrator",
            Email = "admin@sharesync.com",
            PasswordHash = _passwordHasher.HashPassword("Admin123#"),
            Role = "ADMIN",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        // Act
        var loginResponse = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "admin@sharesync.com",
            Password = "Admin123#"
        });

        // Assert
        Assert.True(loginResponse.Success);
        Assert.NotNull(loginResponse.Data);
        Assert.Equal("ADMIN", loginResponse.Data.Role);

        // Verify JWT claim
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(loginResponse.Data.Token);
        var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role");
        Assert.NotNull(roleClaim);
        Assert.Equal("ADMIN", roleClaim.Value);
    }

    [Fact]
    public async Task InvestorLogin_WithInvestorCredentials_GeneratesTokenWithInvestorRole()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var tokenGenerator = new JwtTokenGenerator(config);
        var authService = new AuthService(context, _passwordHasher, tokenGenerator);

        var investorUser = new AppUser
        {
            UserId = 2,
            Name = "Regular Investor",
            Email = "investor@sharesync.com",
            PasswordHash = _passwordHasher.HashPassword("Investor123#"),
            Role = "INVESTOR",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(investorUser);
        await context.SaveChangesAsync();

        // Act
        var loginResponse = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "investor@sharesync.com",
            Password = "Investor123#"
        });

        // Assert
        Assert.True(loginResponse.Success);
        Assert.NotNull(loginResponse.Data);
        Assert.Equal("INVESTOR", loginResponse.Data.Role);

        // Verify JWT claim
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(loginResponse.Data.Token);
        var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role");
        Assert.NotNull(roleClaim);
        Assert.Equal("INVESTOR", roleClaim.Value);
    }

    [Fact]
    public async Task Login_WithDeactivatedUser_Throws403Forbidden()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var tokenGenerator = new JwtTokenGenerator(config);
        var authService = new AuthService(context, _passwordHasher, tokenGenerator);

        var disabledUser = new AppUser
        {
            UserId = 3,
            Name = "Disabled User",
            Email = "disabled@example.com",
            PasswordHash = _passwordHasher.HashPassword("Password123#"),
            Role = "INVESTOR",
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(disabledUser);
        await context.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppException>(() => authService.LoginAsync(new LoginRequestDto
        {
            Email = "disabled@example.com",
            Password = "Password123#"
        }));

        Assert.Equal(403, ex.StatusCode);
        Assert.Contains("deactivated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // 2. COMPANY MANAGEMENT CRUD & SAFE DELETION
    // =========================================================================

    [Fact]
    public async Task CompanyManagement_CreateUpdateAndToggleActive_WorksCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var fakeDse = new FakeDsePriceService();
        var adminService = new AdminService(context, fakeDse);

        var sector = new Sector { SectorId = 1, SectorName = "Banking" };
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        // 1. Create company
        var createRequest = new CreateCompanyRequestDto
        {
            TickerSymbol = "BRACBANK",
            CompanyName = "BRAC Bank PLC",
            SectorId = 1,
            CurrentPrice = 52.40m,
            MarketCap = 15000000m,
            IsActive = true
        };

        var createResult = await adminService.CreateCompanyAsync(createRequest);
        Assert.True(createResult.Success);
        Assert.NotNull(createResult.Data);
        Assert.Equal("BRACBANK", createResult.Data.TickerSymbol);
        Assert.True(createResult.Data.IsActive);

        var companyId = createResult.Data.CompanyId;

        // 2. Update company
        var updateRequest = new UpdateCompanyRequestDto
        {
            CompanyName = "BRAC Bank PLC (Updated)",
            TickerSymbol = "BRACBANK",
            SectorId = 1,
            CurrentPrice = 54.00m,
            MarketCap = 16000000m,
            IsActive = true
        };

        var updateResult = await adminService.UpdateCompanyAsync(companyId, updateRequest);
        Assert.True(updateResult.Success);
        Assert.Equal("BRAC Bank PLC (Updated)", updateResult.Data!.CompanyName);
        Assert.Equal(54.00m, updateResult.Data.CurrentPrice);

        // 3. Toggle status to inactive (Safe deactivation)
        var statusResult = await adminService.ToggleCompanyStatusAsync(companyId);
        Assert.True(statusResult.Success);
        Assert.False(statusResult.Data!.IsActive);

        var dbCompany = await context.Companies.FindAsync(companyId);
        Assert.NotNull(dbCompany);
        Assert.False(dbCompany.IsActive);
    }

    [Fact]
    public async Task DeleteCompany_WhenReferencedByTransactions_BlocksDeletionSafely()
    {
        // Arrange: Guardrail test - Destructive deletion MUST be blocked if company has transaction history
        using var context = CreateInMemoryDbContext();
        var fakeDse = new FakeDsePriceService();
        var adminService = new AdminService(context, fakeDse);

        var company = new Company
        {
            CompanyId = 10,
            TickerSymbol = "GP",
            CompanyName = "Grameenphone Ltd.",
            CurrentPrice = 320.00m,
            IsActive = true
        };
        context.Companies.Add(company);

        var portfolio = new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "Main" };
        context.Portfolios.Add(portfolio);

        var tx = new Transaction
        {
            TransactionId = 1,
            PortfolioId = 1,
            CompanyId = 10,
            TransactionType = "BUY",
            Quantity = 50,
            PricePerShare = 310.00m,
            TransactionDate = DateTime.UtcNow
        };
        context.Transactions.Add(tx);
        await context.SaveChangesAsync();

        // Act & Assert: Should throw 400 Bad Request and preserve record
        var ex = await Assert.ThrowsAsync<AppException>(() => adminService.DeleteCompanyAsync(10));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("transaction", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Company must still exist in database
        var surviving = await context.Companies.FindAsync(10);
        Assert.NotNull(surviving);
    }

    [Fact]
    public async Task DeleteCompany_WhenUnreferenced_DeletesSuccessfully()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var fakeDse = new FakeDsePriceService();
        var adminService = new AdminService(context, fakeDse);

        var unreferencedCompany = new Company
        {
            CompanyId = 99,
            TickerSymbol = "UNUSED",
            CompanyName = "Unused Corp",
            CurrentPrice = 10.00m,
            IsActive = false
        };
        context.Companies.Add(unreferencedCompany);
        await context.SaveChangesAsync();

        // Act
        var result = await adminService.DeleteCompanyAsync(99);

        // Assert
        Assert.True(result.Success);
        var inDb = await context.Companies.FindAsync(99);
        Assert.Null(inDb);
    }

    // =========================================================================
    // 3. SECTOR MANAGEMENT CRUD & SAFE DELETION
    // =========================================================================

    [Fact]
    public async Task SectorManagement_CreateUpdateAndView_WorksCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var fakeDse = new FakeDsePriceService();
        var adminService = new AdminService(context, fakeDse);

        // 1. Create
        var createResult = await adminService.CreateSectorAsync(new CreateSectorRequestDto
        {
            SectorName = "Energy & Power",
            Description = "Power generation and distribution companies"
        });

        Assert.True(createResult.Success);
        Assert.NotNull(createResult.Data);
        Assert.Equal("Energy & Power", createResult.Data.SectorName);

        var sectorId = createResult.Data.SectorId;

        // 2. Update
        var updateResult = await adminService.UpdateSectorAsync(sectorId, new UpdateSectorRequestDto
        {
            SectorName = "Power & Energy",
            Description = "Updated description"
        });

        Assert.True(updateResult.Success);
        Assert.Equal("Power & Energy", updateResult.Data!.SectorName);

        // 3. View list
        var listResult = await adminService.GetAllSectorsAsync();
        Assert.True(listResult.Success);
        Assert.Contains(listResult.Data!, s => s.SectorId == sectorId && s.SectorName == "Power & Energy");
    }

    [Fact]
    public async Task DeleteSector_WhenReferencedByCompanies_BlocksDeletionSafely()
    {
        // Arrange: Guardrail test - Sector referenced by companies cannot be deleted
        using var context = CreateInMemoryDbContext();
        var fakeDse = new FakeDsePriceService();
        var adminService = new AdminService(context, fakeDse);

        var sector = new Sector
        {
            SectorId = 5,
            SectorName = "Pharmaceuticals",
            Description = "Pharma"
        };
        context.Sectors.Add(sector);

        var company = new Company
        {
            CompanyId = 50,
            TickerSymbol = "SQURPHARMA",
            CompanyName = "Square Pharmaceuticals PLC",
            SectorId = 5,
            CurrentPrice = 215.00m,
            IsActive = true
        };
        context.Companies.Add(company);
        await context.SaveChangesAsync();

        // Act & Assert: Must throw 400 Bad Request
        var ex = await Assert.ThrowsAsync<AppException>(() => adminService.DeleteSectorAsync(5));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("associated company", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Sector must still exist
        var survivingSector = await context.Sectors.FindAsync(5);
        Assert.NotNull(survivingSector);
    }

    // =========================================================================
    // 4. USER MANAGEMENT & SECURITY
    // =========================================================================

    [Fact]
    public async Task GetUsers_NeverReturnsPlaintextPasswordsOrHashes()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var fakeDse = new FakeDsePriceService();
        var adminService = new AdminService(context, fakeDse);

        var user = new AppUser
        {
            UserId = 100,
            Name = "Secretive User",
            Email = "secret@example.com",
            PasswordHash = "$2a$11$SensitiveSuperSecretPasswordHashStringHere",
            Role = "INVESTOR",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Act
        var usersResult = await adminService.GetAllUsersAsync();

        // Assert
        Assert.True(usersResult.Success);
        var targetUser = usersResult.Data!.FirstOrDefault(u => u.UserId == 100);
        Assert.NotNull(targetUser);

        // Verify reflection: AdminUserDto must not have any password/hash property
        var properties = typeof(AdminUserDto).GetProperties();
        Assert.DoesNotContain(properties, p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, p => p.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ToggleUserStatusAndRole_UpdatesUserCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var fakeDse = new FakeDsePriceService();
        var adminService = new AdminService(context, fakeDse);

        var user = new AppUser
        {
            UserId = 101,
            Name = "Target User",
            Email = "target@example.com",
            PasswordHash = "hash",
            Role = "INVESTOR",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // 1. Toggle Active -> Inactive
        var statusResult = await adminService.ToggleUserStatusAsync(101, requestingAdminUserId: 1);
        Assert.True(statusResult.Success);
        Assert.False(statusResult.Data!.IsActive);

        // 2. Change Role -> ADMIN
        var roleResult = await adminService.UpdateUserRoleAsync(101, "ADMIN", requestingAdminUserId: 1);
        Assert.True(roleResult.Success);
        Assert.Equal("ADMIN", roleResult.Data!.Role);

        var updatedInDb = await context.Users.FindAsync(101);
        Assert.False(updatedInDb!.IsActive);
        Assert.Equal("ADMIN", updatedInDb.Role);
    }

    // =========================================================================
    // 5. DSE SYNCHRONIZATION VIA ADMIN SERVICE
    // =========================================================================

    [Fact]
    public async Task TriggerDseSync_ReusesDsePriceService_ReturnsResult()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var fakeDse = new FakeDsePriceService();
        var adminService = new AdminService(context, fakeDse);
        var currentUserService = new FakeCurrentUserService(1, "ADMIN");
        var controller = new AdminController(adminService, fakeDse, currentUserService);

        // Act
        var result = await controller.TriggerDseSync(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<DsePriceUpdateResultDto>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(8, apiResponse.Data.UpdatedCount);
        Assert.Contains("Simulated DSE price sync", apiResponse.Message);
    }

    [Fact]
    public async Task Admin_CannotDeactivateOrRevokeOwnAccount()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var fakeDse = new FakeDsePriceService();
        var adminService = new AdminService(context, fakeDse);

        var adminUser = new AppUser
        {
            UserId = 1,
            Name = "Root Admin",
            Email = "root@admin.com",
            PasswordHash = "hash",
            Role = "ADMIN",
            IsActive = true
        };
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        // Act & Assert 1: Self-deactivation forbidden
        var deactEx = await Assert.ThrowsAsync<AppException>(() => adminService.ToggleUserStatusAsync(1, requestingAdminUserId: 1));
        Assert.Equal(400, deactEx.StatusCode);
        Assert.Contains("cannot deactivate your own", deactEx.Message, StringComparison.OrdinalIgnoreCase);

        // Act & Assert 2: Self-demotion forbidden
        var demoteEx = await Assert.ThrowsAsync<AppException>(() => adminService.UpdateUserRoleAsync(1, "INVESTOR", requestingAdminUserId: 1));
        Assert.Equal(400, demoteEx.StatusCode);
        Assert.Contains("cannot revoke your own", demoteEx.Message, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // 6. SYSTEM STATISTICS & AUDIT LOGS
    // =========================================================================

    [Fact]
    public async Task GetDashboardOverview_ReturnsAccurateSystemAggregates()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var fakeDse = new FakeDsePriceService();
        var adminService = new AdminService(context, fakeDse);

        context.Users.AddRange(
            new AppUser { UserId = 1, Name = "U1", Email = "u1@e.com", PasswordHash = "h", IsActive = true, Role = "ADMIN" },
            new AppUser { UserId = 2, Name = "U2", Email = "u2@e.com", PasswordHash = "h", IsActive = false, Role = "INVESTOR" }
        );

        context.Sectors.Add(new Sector { SectorId = 1, SectorName = "IT" });

        context.Companies.AddRange(
            new Company { CompanyId = 1, TickerSymbol = "COMP1", CompanyName = "C1", IsActive = true },
            new Company { CompanyId = 2, TickerSymbol = "COMP2", CompanyName = "C2", IsActive = false }
        );

        context.Portfolios.Add(new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "P1" });

        context.Transactions.Add(new Transaction
        {
            TransactionId = 1,
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 10,
            PricePerShare = 100,
            TransactionDate = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        // Act
        var overviewResult = await adminService.GetDashboardStatsAsync();

        // Assert
        Assert.True(overviewResult.Success);
        var stats = overviewResult.Data!;
        Assert.Equal(2, stats.TotalUsers);
        Assert.Equal(1, stats.ActiveUsers);
        Assert.Equal(2, stats.TotalCompanies);
        Assert.Equal(1, stats.ActiveCompanies);
        Assert.Equal(1, stats.TotalSectors);
        Assert.Equal(1, stats.TotalPortfolios);
        Assert.Equal(1, stats.TotalTransactions);
    }

    [Fact]
    public async Task GetAuditLogs_ReturnsRecentAuditRecords()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var fakeDse = new FakeDsePriceService();
        var adminService = new AdminService(context, fakeDse);

        context.TransactionAudits.Add(new TransactionAudit
        {
            AuditId = 1,
            TransactionId = 123,
            ActionType = "INSERT",
            ActionDate = DateTime.UtcNow,
            ChangedBy = "SYSTEM",
            Details = "Transaction logged via unit test"
        });
        await context.SaveChangesAsync();

        // Act
        var logsResult = await adminService.GetAuditLogsAsync();

        // Assert
        Assert.True(logsResult.Success);
        Assert.NotEmpty(logsResult.Data!);
        Assert.Equal("INSERT", logsResult.Data![0].ActionType);
        Assert.Equal(123, logsResult.Data[0].TransactionId);
    }
}
