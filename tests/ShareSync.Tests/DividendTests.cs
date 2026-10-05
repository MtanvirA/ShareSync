using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.DTOs.Dividends;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class DividendTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new ShareSyncDbContext(options);

        // Seed Sectors
        context.Sectors.Add(new Sector { SectorId = 1, SectorName = "Telecommunications" });

        // Seed Companies
        context.Companies.AddRange(
            new Company { CompanyId = 1, TickerSymbol = "BX", CompanyName = "Beximco Limited", CurrentPrice = 120.00m, SectorId = 1 },
            new Company { CompanyId = 2, TickerSymbol = "GP", CompanyName = "Grameenphone Ltd.", CurrentPrice = 295.00m, SectorId = 1 },
            new Company { CompanyId = 3, TickerSymbol = "BATBC", CompanyName = "BAT Bangladesh", CurrentPrice = 450.00m, SectorId = 1 }
        );

        // Seed a Portfolio and Transactions for User 1
        var portfolio = new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "Test Portfolio" };
        context.Portfolios.Add(portfolio);

        // User 1 holds 100 shares of Company 1 (BX) and 50 shares of Company 2 (GP)
        context.Transactions.AddRange(
            new Transaction
            {
                TransactionId = 1,
                PortfolioId = 1,
                Portfolio = portfolio,
                CompanyId = 1,
                TransactionType = "BUY",
                Quantity = 100,
                PricePerShare = 110,
                TransactionDate = new DateTime(2026, 1, 15)
            },
            new Transaction
            {
                TransactionId = 2,
                PortfolioId = 1,
                Portfolio = portfolio,
                CompanyId = 2,
                TransactionType = "BUY",
                Quantity = 50,
                PricePerShare = 280,
                TransactionDate = new DateTime(2026, 1, 20)
            }
        );

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task CreateDividend_WithValidData_ReturnsSuccessAndPersists()
    {
        using var context = CreateInMemoryDbContext();
        var service = new DividendService(context);
        const int userId = 1;

        var request = new CreateDividendRequestDto
        {
            CompanyId = 1,
            DividendPerShare = 5.50m,
            DeclarationDate = new DateTime(2026, 6, 1),
            PaymentDate = new DateTime(2026, 6, 30)
        };

        var response = await service.CreateDividendAsync(request, userId);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(1, response.Data.CompanyId);
        Assert.Equal("BX", response.Data.TickerSymbol);
        Assert.Equal(5.50m, response.Data.DividendPerShare);
        Assert.Equal(100, response.Data.UserSharesHeld);
        Assert.Equal(550.00m, response.Data.EstimatedIncome);

        var saved = await context.Dividends.FirstOrDefaultAsync(d => d.DividendId == response.Data.DividendId);
        Assert.NotNull(saved);
        Assert.Equal(5.50m, saved.DividendPerShare);
    }

    [Fact]
    public async Task CreateDividend_WithInvalidCompany_ThrowsNotFound()
    {
        using var context = CreateInMemoryDbContext();
        var service = new DividendService(context);

        var request = new CreateDividendRequestDto
        {
            CompanyId = 9999,
            DividendPerShare = 4.00m,
            DeclarationDate = new DateTime(2026, 5, 1),
            PaymentDate = new DateTime(2026, 5, 20)
        };

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateDividendAsync(request, userId: 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5.50)]
    public async Task CreateDividend_WithZeroOrNegativeAmount_ThrowsBadRequest(decimal invalidAmount)
    {
        using var context = CreateInMemoryDbContext();
        var service = new DividendService(context);

        var request = new CreateDividendRequestDto
        {
            CompanyId = 1,
            DividendPerShare = invalidAmount,
            DeclarationDate = new DateTime(2026, 5, 1),
            PaymentDate = new DateTime(2026, 5, 20)
        };

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateDividendAsync(request, userId: 1));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CreateDividend_WithPaymentDateBeforeDeclarationDate_ThrowsBadRequest()
    {
        using var context = CreateInMemoryDbContext();
        var service = new DividendService(context);

        var request = new CreateDividendRequestDto
        {
            CompanyId = 1,
            DividendPerShare = 3.00m,
            DeclarationDate = new DateTime(2026, 7, 15),
            PaymentDate = new DateTime(2026, 7, 10) // 5 days before declaration
        };

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateDividendAsync(request, userId: 1));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task GetDividendById_WithValidId_ReturnsDetail()
    {
        using var context = CreateInMemoryDbContext();
        var service = new DividendService(context);

        var created = await service.CreateDividendAsync(new CreateDividendRequestDto
        {
            CompanyId = 2,
            DividendPerShare = 10.00m,
            DeclarationDate = new DateTime(2026, 8, 1),
            PaymentDate = new DateTime(2026, 8, 25)
        }, userId: 1);

        var detail = await service.GetDividendByIdAsync(created.Data!.DividendId, userId: 1);

        Assert.True(detail.Success);
        Assert.Equal("GP", detail.Data!.TickerSymbol);
        Assert.Equal(10.00m, detail.Data.DividendPerShare);
        Assert.Equal(50, detail.Data.UserSharesHeld);
        Assert.Equal(500.00m, detail.Data.EstimatedIncome);
    }

    [Fact]
    public async Task GetDividendById_WithNonexistentId_ThrowsNotFound()
    {
        using var context = CreateInMemoryDbContext();
        var service = new DividendService(context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetDividendByIdAsync(99999, userId: 1));
    }

    [Fact]
    public async Task UpdateDividend_WithValidData_UpdatesSuccessfully()
    {
        using var context = CreateInMemoryDbContext();
        var service = new DividendService(context);

        var created = await service.CreateDividendAsync(new CreateDividendRequestDto
        {
            CompanyId = 1,
            DividendPerShare = 2.00m,
            DeclarationDate = new DateTime(2026, 4, 1),
            PaymentDate = new DateTime(2026, 4, 25)
        }, userId: 1);

        var updateReq = new UpdateDividendRequestDto
        {
            CompanyId = 1,
            DividendPerShare = 3.50m,
            DeclarationDate = new DateTime(2026, 4, 1),
            PaymentDate = new DateTime(2026, 4, 28)
        };

        var updated = await service.UpdateDividendAsync(created.Data!.DividendId, updateReq, userId: 1);

        Assert.True(updated.Success);
        Assert.Equal(3.50m, updated.Data!.DividendPerShare);
        Assert.Equal(350.00m, updated.Data.EstimatedIncome);

        var inDb = await context.Dividends.FindAsync(created.Data.DividendId);
        Assert.Equal(3.50m, inDb!.DividendPerShare);
    }

    [Fact]
    public async Task UpdateDividend_WithPaymentDateBeforeDeclarationDate_ThrowsBadRequest()
    {
        using var context = CreateInMemoryDbContext();
        var service = new DividendService(context);

        var created = await service.CreateDividendAsync(new CreateDividendRequestDto
        {
            CompanyId = 1,
            DividendPerShare = 2.00m,
            DeclarationDate = new DateTime(2026, 4, 1),
            PaymentDate = new DateTime(2026, 4, 25)
        }, userId: 1);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.UpdateDividendAsync(created.Data!.DividendId, new UpdateDividendRequestDto
            {
                CompanyId = 1,
                DividendPerShare = 2.00m,
                DeclarationDate = new DateTime(2026, 4, 10),
                PaymentDate = new DateTime(2026, 4, 5)
            }, userId: 1));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task DeleteDividend_WithValidId_DeletesSuccessfully()
    {
        using var context = CreateInMemoryDbContext();
        var service = new DividendService(context);

        var created = await service.CreateDividendAsync(new CreateDividendRequestDto
        {
            CompanyId = 1,
            DividendPerShare = 2.00m,
            DeclarationDate = new DateTime(2026, 4, 1),
            PaymentDate = new DateTime(2026, 4, 25)
        }, userId: 1);

        var res = await service.DeleteDividendAsync(created.Data!.DividendId, userId: 1);
        Assert.True(res.Success);

        var inDb = await context.Dividends.FindAsync(created.Data.DividendId);
        Assert.Null(inDb);
    }

    [Fact]
    public async Task DeleteDividend_WithNonexistentId_ThrowsNotFound()
    {
        using var context = CreateInMemoryDbContext();
        var service = new DividendService(context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.DeleteDividendAsync(9999, userId: 1));
    }

    [Fact]
    public async Task FilterDividends_ByCompany_ReturnsOnlyMatchingCompany()
    {
        using var context = CreateInMemoryDbContext();
        var service = new DividendService(context);

        await service.CreateDividendAsync(new CreateDividendRequestDto
        {
            CompanyId = 1,
            DividendPerShare = 2.00m,
            DeclarationDate = new DateTime(2026, 3, 1),
            PaymentDate = new DateTime(2026, 3, 20)
        }, userId: 1);

        await service.CreateDividendAsync(new CreateDividendRequestDto
        {
            CompanyId = 2,
            DividendPerShare = 5.00m,
            DeclarationDate = new DateTime(2026, 3, 5),
            PaymentDate = new DateTime(2026, 3, 25)
        }, userId: 1);

        var filter = new DividendFilterDto { CompanyId = 1 };
        var list = await service.GetDividendsAsync(filter, userId: 1);

        Assert.Single(list.Data!);
        Assert.Equal("BX", list.Data![0].TickerSymbol);
    }

    [Fact]
    public async Task FilterDividends_ByYear_ReturnsOnlyMatchingYear()
    {
        using var context = CreateInMemoryDbContext();
        var service = new DividendService(context);

        await service.CreateDividendAsync(new CreateDividendRequestDto
        {
            CompanyId = 1,
            DividendPerShare = 2.00m,
            DeclarationDate = new DateTime(2025, 5, 1),
            PaymentDate = new DateTime(2025, 5, 20)
        }, userId: 1);

        await service.CreateDividendAsync(new CreateDividendRequestDto
        {
            CompanyId = 1,
            DividendPerShare = 3.00m,
            DeclarationDate = new DateTime(2026, 5, 1),
            PaymentDate = new DateTime(2026, 5, 20)
        }, userId: 1);

        var filter = new DividendFilterDto { Year = 2026 };
        var list = await service.GetDividendsAsync(filter, userId: 1);

        Assert.Single(list.Data!);
        Assert.Equal(2026, list.Data![0].PaymentDate.Year);
    }

    [Fact]
    public async Task GetDividendSummary_CalculatesAccurateTotals()
    {
        using var context = CreateInMemoryDbContext();
        var service = new DividendService(context);

        // User 1 holds: 100 shares of Comp 1 (BX), 50 shares of Comp 2 (GP), 0 shares of Comp 3 (BATBC)
        await service.CreateDividendAsync(new CreateDividendRequestDto
        {
            CompanyId = 1,
            DividendPerShare = 4.00m, // 100 * 4 = 400
            DeclarationDate = new DateTime(2026, 1, 1),
            PaymentDate = new DateTime(2026, 1, 20)
        }, userId: 1);

        await service.CreateDividendAsync(new CreateDividendRequestDto
        {
            CompanyId = 2,
            DividendPerShare = 6.00m, // 50 * 6 = 300
            DeclarationDate = new DateTime(2026, 2, 1),
            PaymentDate = new DateTime(2026, 2, 20)
        }, userId: 1);

        await service.CreateDividendAsync(new CreateDividendRequestDto
        {
            CompanyId = 3,
            DividendPerShare = 15.00m, // 0 shares = 0
            DeclarationDate = new DateTime(2026, 3, 1),
            PaymentDate = new DateTime(2026, 3, 20)
        }, userId: 1);

        var summary = await service.GetDividendSummaryAsync(userId: 1);

        Assert.True(summary.Success);
        Assert.Equal(700.00m, summary.Data!.TotalIncome); // 400 + 300
        Assert.Equal(3, summary.Data.CompaniesCount);
        Assert.Equal(3, summary.Data.CompanySummaries.Count);

        var bxSummary = summary.Data.CompanySummaries.First(c => c.TickerSymbol == "BX");
        Assert.Equal(400.00m, bxSummary.TotalEstimatedIncome);
        Assert.Equal(100, bxSummary.UserSharesHeld);
    }

    [Fact]
    public async Task GetDividendAnalytics_ReturnsAccurateTotals_Monthly_Company_Sector_AndUpcoming()
    {
        using var context = CreateInMemoryDbContext();
        var service = new DividendService(context);

        // Comp 1 (BX): User 1 holds 100 shares. Dividend: 4.00 on 2026-01-20 (Income = 400)
        await service.CreateDividendAsync(new CreateDividendRequestDto
        {
            CompanyId = 1,
            DividendPerShare = 4.00m,
            DeclarationDate = new DateTime(2026, 1, 1),
            PaymentDate = new DateTime(2026, 1, 20)
        }, userId: 1);

        // Comp 2 (GP): User 1 holds 50 shares. Dividend: 6.00 on 2026-02-15 (Income = 300)
        await service.CreateDividendAsync(new CreateDividendRequestDto
        {
            CompanyId = 2,
            DividendPerShare = 6.00m,
            DeclarationDate = new DateTime(2026, 2, 1),
            PaymentDate = new DateTime(2026, 2, 15)
        }, userId: 1);

        // Comp 1 (BX): Future dividend on 2028-12-01 (Income = 250)
        await service.CreateDividendAsync(new CreateDividendRequestDto
        {
            CompanyId = 1,
            DividendPerShare = 2.50m,
            DeclarationDate = new DateTime(2028, 11, 1),
            PaymentDate = new DateTime(2028, 12, 1)
        }, userId: 1);

        var filter = new DividendAnalyticsFilterDto();
        var result = await service.GetDividendAnalyticsAsync(filter, userId: 1);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var data = result.Data;

        // 1. Total Dividend Income: 400 + 300 + 250 = 950
        Assert.Equal(950.00m, data.TotalDividendIncome);
        Assert.Equal(3, data.TotalPaymentsCount);
        Assert.Equal(2, data.CompaniesCount);

        // 2. Upcoming Income: 250 (from 2028 payment)
        Assert.Equal(250.00m, data.UpcomingIncome);
        Assert.Single(data.UpcomingDividends);
        Assert.Equal("BX", data.UpcomingDividends[0].TickerSymbol);
        Assert.Equal(250.00m, data.UpcomingDividends[0].EstimatedIncome);
        Assert.True(data.UpcomingDividends[0].DaysUntilPayment > 0);

        // 3. Monthly Income for current year (2026) has 12 buckets
        Assert.Equal(12, data.MonthlyIncome.Count);
        var jan = data.MonthlyIncome.First(m => m.Month == 1);
        Assert.Equal(400.00m, jan.Income);
        var feb = data.MonthlyIncome.First(m => m.Month == 2);
        Assert.Equal(300.00m, feb.Income);

        // 4. Company Breakdown
        Assert.Equal(2, data.CompanyBreakdown.Count);
        var bxCompany = data.CompanyBreakdown.First(c => c.TickerSymbol == "BX");
        Assert.Equal(650.00m, bxCompany.TotalIncome); // 400 + 250
        Assert.Equal(100, bxCompany.UserSharesHeld);
        Assert.True(bxCompany.DividendYield > 0);

        // 5. Sector Breakdown
        Assert.NotEmpty(data.SectorBreakdown);
        Assert.Equal(950.00m, data.SectorBreakdown.Sum(s => s.TotalIncome));

        // 6. Dividend Yield
        Assert.True(data.AverageDividendYield > 0);
    }

    [Fact]
    public async Task GetDividendAnalytics_PortfolioFilter_ScopesIncomeToSelectedPortfolio()
    {
        using var context = CreateInMemoryDbContext();
        var service = new DividendService(context);

        // Create second portfolio for User 1 with 20 shares of Comp 1 (BX)
        var portfolio2 = new Portfolio { PortfolioId = 2, UserId = 1, PortfolioName = "Second Portfolio" };
        context.Portfolios.Add(portfolio2);
        context.Transactions.Add(new Transaction
        {
            TransactionId = 10,
            PortfolioId = 2,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 20,
            PricePerShare = 115,
            TransactionDate = new DateTime(2026, 1, 10)
        });
        await context.SaveChangesAsync();

        // Comp 1 (BX) pays 5.00 per share
        await service.CreateDividendAsync(new CreateDividendRequestDto
        {
            CompanyId = 1,
            DividendPerShare = 5.00m,
            DeclarationDate = new DateTime(2026, 1, 1),
            PaymentDate = new DateTime(2026, 1, 20)
        }, userId: 1);

        // 1. Scoped to Portfolio 1 (holds 100 shares -> 500 BDT)
        var p1Result = await service.GetDividendAnalyticsAsync(new DividendAnalyticsFilterDto { PortfolioId = 1 }, userId: 1);
        Assert.Equal(500.00m, p1Result.Data!.TotalDividendIncome);

        // 2. Scoped to Portfolio 2 (holds 20 shares -> 100 BDT)
        var p2Result = await service.GetDividendAnalyticsAsync(new DividendAnalyticsFilterDto { PortfolioId = 2 }, userId: 1);
        Assert.Equal(100.00m, p2Result.Data!.TotalDividendIncome);

        // 3. Consolidated (All portfolios -> holds 120 shares -> 600 BDT)
        var allResult = await service.GetDividendAnalyticsAsync(new DividendAnalyticsFilterDto(), userId: 1);
        Assert.Equal(600.00m, allResult.Data!.TotalDividendIncome);
    }

    [Fact]
    public async Task GetDividendAnalytics_CompanyFilter_FiltersByCompany()
    {
        using var context = CreateInMemoryDbContext();
        var service = new DividendService(context);

        await service.CreateDividendAsync(new CreateDividendRequestDto
        {
            CompanyId = 1,
            DividendPerShare = 4.00m,
            DeclarationDate = new DateTime(2026, 1, 1),
            PaymentDate = new DateTime(2026, 1, 20)
        }, userId: 1);

        await service.CreateDividendAsync(new CreateDividendRequestDto
        {
            CompanyId = 2,
            DividendPerShare = 6.00m,
            DeclarationDate = new DateTime(2026, 2, 1),
            PaymentDate = new DateTime(2026, 2, 15)
        }, userId: 1);

        var filter = new DividendAnalyticsFilterDto { CompanyId = 2 };
        var res = await service.GetDividendAnalyticsAsync(filter, userId: 1);

        Assert.True(res.Success);
        Assert.Single(res.Data!.DividendHistory);
        Assert.Equal(2, res.Data.DividendHistory[0].CompanyId);
        Assert.Equal(300.00m, res.Data.TotalDividendIncome);
    }

    [Fact]
    public async Task GetDividendAnalytics_UserIsolation_ThrowsForbiddenWhenAccessingOtherUserPortfolio()
    {
        using var context = CreateInMemoryDbContext();
        var service = new DividendService(context);

        // Create portfolio belonging to User 2
        var pOther = new Portfolio { PortfolioId = 99, UserId = 2, PortfolioName = "Other User Portfolio" };
        context.Portfolios.Add(pOther);
        await context.SaveChangesAsync();

        // User 1 attempts to access User 2's portfolio analytics
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.GetDividendAnalyticsAsync(new DividendAnalyticsFilterDto { PortfolioId = 99 }, userId: 1));
        Assert.Equal(403, ex.StatusCode);
    }
}
