using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Goals;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using ShareSync.Web.Controllers;
using Xunit;

namespace ShareSync.Tests;

public class PortfolioGoalTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ShareSyncDbContext(options);
    }

    [Fact]
    public async Task CreateGoal_ValidTargetPortfolioValue_SucceedsAndPersists()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        const int userId = 1;
        const int portfolioId = 10;

        var user = new AppUser { UserId = userId, Name = "Goal Setter", Email = "goals@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        var portfolio = new Portfolio { PortfolioId = portfolioId, UserId = userId, PortfolioName = "Retirement Fund", User = user };

        context.Users.Add(user);
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync();

        var service = new PortfolioGoalService(context);

        var request = new CreatePortfolioGoalRequestDto
        {
            PortfolioId = portfolioId,
            GoalType = "TARGET_PORTFOLIO_VALUE",
            TargetValue = 500000.00m,
            TargetDate = DateTime.UtcNow.AddYears(1),
            Title = "Reach 500k Portfolio",
            Description = "Long term wealth building"
        };

        // Act
        var response = await service.CreateGoalAsync(request, userId);

        // Assert
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("Reach 500k Portfolio", response.Data.Title);
        Assert.Equal("TARGET_PORTFOLIO_VALUE", response.Data.GoalType);
        Assert.Equal(500000.00m, response.Data.TargetValue);
        Assert.Equal(0m, response.Data.CurrentValue);
        Assert.Equal(0m, response.Data.ProgressPercentage);
        Assert.Equal(500000.00m, response.Data.RemainingValue);
        Assert.True(response.Data.IsActive);

        var saved = await context.PortfolioGoals.FirstOrDefaultAsync(g => g.Title == "Reach 500k Portfolio");
        Assert.NotNull(saved);
        Assert.Equal(portfolioId, saved.PortfolioId);
        Assert.Equal(userId, saved.UserId);
    }

    [Fact]
    public async Task CreateGoal_InvalidTargetValueOrDate_ThrowsAppException()
    {
        using var context = CreateInMemoryDbContext();
        const int userId = 1;
        const int portfolioId = 10;

        var user = new AppUser { UserId = userId, Name = "User", Email = "u@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        var portfolio = new Portfolio { PortfolioId = portfolioId, UserId = userId, PortfolioName = "P1", User = user };

        context.Users.Add(user);
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync();

        var service = new PortfolioGoalService(context);

        // Zero Target Value
        var reqZeroTarget = new CreatePortfolioGoalRequestDto
        {
            PortfolioId = portfolioId,
            GoalType = "TARGET_PORTFOLIO_VALUE",
            TargetValue = 0m,
            Title = "Invalid Goal"
        };
        await Assert.ThrowsAsync<AppException>(() => service.CreateGoalAsync(reqZeroTarget, userId));

        // Past Target Date
        var reqPastDate = new CreatePortfolioGoalRequestDto
        {
            PortfolioId = portfolioId,
            GoalType = "TARGET_PORTFOLIO_VALUE",
            TargetValue = 10000m,
            TargetDate = DateTime.UtcNow.AddDays(-10),
            Title = "Past Goal"
        };
        await Assert.ThrowsAsync<AppException>(() => service.CreateGoalAsync(reqPastDate, userId));
    }

    [Fact]
    public async Task CreateGoal_PortfolioBelongingToAnotherUser_ThrowsForbidden()
    {
        using var context = CreateInMemoryDbContext();
        const int ownerId = 1;
        const int unauthorizedId = 2;
        const int portfolioId = 10;

        var owner = new AppUser { UserId = ownerId, Name = "Owner", Email = "owner@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        var unauthorized = new AppUser { UserId = unauthorizedId, Name = "Hacker", Email = "hacker@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        var portfolio = new Portfolio { PortfolioId = portfolioId, UserId = ownerId, PortfolioName = "Owner Portfolio", User = owner };

        context.Users.AddRange(owner, unauthorized);
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync();

        var service = new PortfolioGoalService(context);

        var request = new CreatePortfolioGoalRequestDto
        {
            PortfolioId = portfolioId,
            GoalType = "TARGET_PORTFOLIO_VALUE",
            TargetValue = 100000m,
            Title = "Intruder Goal"
        };

        var ex = await Assert.ThrowsAsync<AppException>(() => service.CreateGoalAsync(request, unauthorizedId));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task GetUserGoals_DynamicallyCalculatesProgressFromAuthoritativeHoldings()
    {
        // Arrange
        // Portfolio holds: 100 shares of GP @ current price 300 = 30,000 market value
        // Target value is 50,000
        // Progress should be: 30,000 / 50,000 * 100 = 60.00%
        // Remaining should be: 20,000
        using var context = CreateInMemoryDbContext();
        const int userId = 1;
        const int portfolioId = 10;
        const int companyId = 101;

        var user = new AppUser { UserId = userId, Name = "Investor", Email = "investor@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
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

        var goal = new PortfolioGoal
        {
            GoalId = 1,
            UserId = userId,
            PortfolioId = portfolioId,
            GoalType = "TARGET_PORTFOLIO_VALUE",
            TargetValue = 50000.00m,
            TargetDate = DateTime.UtcNow.AddMonths(6),
            Title = "Reach 50k",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };
        context.PortfolioGoals.Add(goal);
        await context.SaveChangesAsync();

        var service = new PortfolioGoalService(context);

        // Act
        var response = await service.GetUserGoalsAsync(userId);

        // Assert
        Assert.True(response.Success);
        var dto = Assert.Single(response.Data!);
        Assert.Equal(30000.00m, dto.CurrentValue);
        Assert.Equal(50000.00m, dto.TargetValue);
        Assert.Equal(60.00m, dto.ProgressPercentage);
        Assert.Equal(20000.00m, dto.RemainingValue);
        Assert.Equal("ON_TRACK", dto.Status);
    }

    [Fact]
    public async Task GetUserGoals_AchievedGoal_SetsStatusToAchieved()
    {
        // Target: 25,000. Current Value: 100 * 300 = 30,000 (120% progress)
        using var context = CreateInMemoryDbContext();
        const int userId = 1;
        const int portfolioId = 10;
        const int companyId = 101;

        var user = new AppUser { UserId = userId, Name = "Investor", Email = "investor@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        var company = new Company { CompanyId = companyId, CompanyName = "GP", TickerSymbol = "GP", CurrentPrice = 300.00m };
        var portfolio = new Portfolio { PortfolioId = portfolioId, UserId = userId, PortfolioName = "Main Portfolio", User = user };

        context.Users.Add(user);
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
            Company = company,
            Portfolio = portfolio
        });

        var goal = new PortfolioGoal
        {
            GoalId = 1,
            UserId = userId,
            PortfolioId = portfolioId,
            GoalType = "TARGET_PORTFOLIO_VALUE",
            TargetValue = 25000.00m,
            Title = "Reach 25k",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };
        context.PortfolioGoals.Add(goal);
        await context.SaveChangesAsync();

        var service = new PortfolioGoalService(context);

        // Act
        var response = await service.GetUserGoalsAsync(userId);

        // Assert
        Assert.True(response.Success);
        var dto = Assert.Single(response.Data!);
        Assert.Equal(120.00m, dto.ProgressPercentage);
        Assert.Equal(0m, dto.RemainingValue);
        Assert.Equal("ACHIEVED", dto.Status);
    }

    [Fact]
    public async Task UserIsolation_CannotAccessOrModifyAnotherUsersGoal()
    {
        using var context = CreateInMemoryDbContext();
        const int ownerId = 1;
        const int otherUserId = 2;
        const int portfolioId = 10;

        var owner = new AppUser { UserId = ownerId, Name = "Owner", Email = "owner@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        var otherUser = new AppUser { UserId = otherUserId, Name = "Other", Email = "other@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        var portfolio = new Portfolio { PortfolioId = portfolioId, UserId = ownerId, PortfolioName = "P1", User = owner };

        context.Users.AddRange(owner, otherUser);
        context.Portfolios.Add(portfolio);

        var goal = new PortfolioGoal
        {
            GoalId = 100,
            UserId = ownerId,
            PortfolioId = portfolioId,
            GoalType = "TARGET_PORTFOLIO_VALUE",
            TargetValue = 100000m,
            Title = "Secret Wealth Goal",
            IsActive = true
        };
        context.PortfolioGoals.Add(goal);
        await context.SaveChangesAsync();

        var service = new PortfolioGoalService(context);

        // Act & Assert GetById
        var exGet = await Assert.ThrowsAsync<AppException>(() => service.GetGoalByIdAsync(100, otherUserId));
        Assert.Equal(403, exGet.StatusCode);

        // Act & Assert Update
        var updateReq = new UpdatePortfolioGoalRequestDto { Title = "Hacked Title", TargetValue = 1000m };
        var exUpdate = await Assert.ThrowsAsync<AppException>(() => service.UpdateGoalAsync(100, updateReq, otherUserId));
        Assert.Equal(403, exUpdate.StatusCode);

        // Act & Assert Delete
        var exDelete = await Assert.ThrowsAsync<AppException>(() => service.DeleteGoalAsync(100, otherUserId));
        Assert.Equal(403, exDelete.StatusCode);
    }

    [Fact]
    public async Task UpdateGoal_AndDeactivate_WorksCorrectly()
    {
        using var context = CreateInMemoryDbContext();
        const int userId = 1;
        const int portfolioId = 10;

        var user = new AppUser { UserId = userId, Name = "User", Email = "u@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        var portfolio = new Portfolio { PortfolioId = portfolioId, UserId = userId, PortfolioName = "P1", User = user };

        context.Users.Add(user);
        context.Portfolios.Add(portfolio);

        var goal = new PortfolioGoal
        {
            GoalId = 1,
            UserId = userId,
            PortfolioId = portfolioId,
            GoalType = "TARGET_PORTFOLIO_VALUE",
            TargetValue = 100000m,
            Title = "Original Goal",
            IsActive = true
        };
        context.PortfolioGoals.Add(goal);
        await context.SaveChangesAsync();

        var service = new PortfolioGoalService(context);

        // Update target value and deactivate
        var updateReq = new UpdatePortfolioGoalRequestDto
        {
            Title = "Updated Goal",
            TargetValue = 200000m,
            IsActive = false
        };

        var response = await service.UpdateGoalAsync(1, updateReq, userId);

        Assert.True(response.Success);
        Assert.Equal("Updated Goal", response.Data!.Title);
        Assert.Equal(200000m, response.Data.TargetValue);
        Assert.False(response.Data.IsActive);

        var saved = await context.PortfolioGoals.FindAsync(1);
        Assert.NotNull(saved);
        Assert.False(saved.IsActive);
        Assert.NotNull(saved.UpdatedAt);
    }

    [Fact]
    public async Task GoalsController_Endpoints_ReturnExpectedResults()
    {
        using var context = CreateInMemoryDbContext();
        const int userId = 1;
        const int portfolioId = 10;

        var user = new AppUser { UserId = userId, Name = "User", Email = "u@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        var portfolio = new Portfolio { PortfolioId = portfolioId, UserId = userId, PortfolioName = "P1", User = user };

        context.Users.Add(user);
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync();

        var service = new PortfolioGoalService(context);
        var fakeUser = new FakeCurrentUserService(userId: userId);
        var controller = new GoalsController(service, fakeUser);

        // 1. Create Goal
        var createReq = new CreatePortfolioGoalRequestDto
        {
            PortfolioId = portfolioId,
            GoalType = "TARGET_PORTFOLIO_VALUE",
            TargetValue = 75000m,
            Title = "Controller Test Goal"
        };

        var createResult = await controller.CreateGoal(createReq, default);
        var createdAction = Assert.IsType<CreatedAtActionResult>(createResult);
        var createdResp = Assert.IsType<ApiResponse<PortfolioGoalDto>>(createdAction.Value);
        Assert.True(createdResp.Success);
        var goalId = createdResp.Data!.GoalId;

        // 2. Get Goals
        var getResult = await controller.GetGoals(null, false, default);
        var okResult = Assert.IsType<OkObjectResult>(getResult);
        var getResp = Assert.IsType<ApiResponse<List<PortfolioGoalDto>>>(okResult.Value);
        Assert.True(getResp.Success);
        Assert.Single(getResp.Data!);

        // 3. Delete Goal
        var deleteResult = await controller.DeleteGoal(goalId, default);
        var deleteOk = Assert.IsType<OkObjectResult>(deleteResult);
        var deleteResp = Assert.IsType<ApiResponse>(deleteOk.Value);
        Assert.True(deleteResp.Success);
    }

    [Fact]
    public async Task GetUserGoals_WhenNoGoalsExist_ReturnsSuccessWithEmptyList()
    {
        using var context = CreateInMemoryDbContext();
        const int userId = 1;
        var user = new AppUser { UserId = userId, Name = "Goal Setter", Email = "goals@sharesync.com", PasswordHash = "hash", Role = "INVESTOR" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = new PortfolioGoalService(context);
        var fakeUser = new FakeCurrentUserService(userId: userId);
        var controller = new GoalsController(service, fakeUser);

        var result = await controller.GetGoals(null, false, default);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var resp = Assert.IsType<ApiResponse<List<PortfolioGoalDto>>>(okResult.Value);

        Assert.True(resp.Success);
        Assert.NotNull(resp.Data);
        Assert.Empty(resp.Data);
    }

    private class FakeCurrentUserService : ICurrentUserService
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
