using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.DTOs.Alerts;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class AlertTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ShareSyncDbContext(options);
    }

    private AlertService CreateAlertService(ShareSyncDbContext context)
    {
        return new AlertService(context, NullLogger<AlertService>.Instance);
    }

    [Fact]
    public async Task CreateAlert_PriceAbove_Success()
    {
        using var context = CreateInMemoryDbContext();
        int userId = 1;
        var company = new Company
        {
            CompanyId = 10,
            TickerSymbol = "BEXIMCO",
            CompanyName = "Beximco Ltd",
            CurrentPrice = 115.00m
        };
        context.Companies.Add(company);
        await context.SaveChangesAsync();

        var service = CreateAlertService(context);

        var request = new CreateAlertRequestDto
        {
            AlertType = "PRICE_ABOVE",
            CompanyId = 10,
            ThresholdValue = 130.00m
        };

        var response = await service.CreateAlertAsync(request, userId);

        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("PRICE_ABOVE", response.Data.AlertType);
        Assert.Equal(10, response.Data.CompanyId);
        Assert.Equal("BEXIMCO", response.Data.TickerSymbol);
        Assert.Equal(130.00m, response.Data.ThresholdValue);
        Assert.True(response.Data.IsActive);
        Assert.Null(response.Data.TriggeredAt);
        Assert.Equal("ACTIVE", response.Data.Status);
    }

    [Fact]
    public async Task CreateAlert_PortfolioValueAbove_Success()
    {
        using var context = CreateInMemoryDbContext();
        int userId = 1;
        var portfolio = new Portfolio
        {
            PortfolioId = 100,
            UserId = userId,
            PortfolioName = "Main Retirement"
        };
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync();

        var service = CreateAlertService(context);

        var request = new CreateAlertRequestDto
        {
            AlertType = "PORTFOLIO_VALUE_ABOVE",
            PortfolioId = 100,
            ThresholdValue = 500000.00m
        };

        var response = await service.CreateAlertAsync(request, userId);

        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("PORTFOLIO_VALUE_ABOVE", response.Data.AlertType);
        Assert.Equal(100, response.Data.PortfolioId);
        Assert.Equal("Main Retirement", response.Data.PortfolioName);
        Assert.Equal(500000.00m, response.Data.ThresholdValue);
        Assert.True(response.Data.IsActive);
    }

    [Fact]
    public async Task CreateAlert_PriceAlertWithoutCompany_ThrowsBadRequest()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateAlertService(context);

        var request = new CreateAlertRequestDto
        {
            AlertType = "PRICE_ABOVE",
            CompanyId = null,
            ThresholdValue = 150.00m
        };

        var ex = await Assert.ThrowsAsync<AppException>(() => service.CreateAlertAsync(request, 1));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CreateAlert_PriceAlertWithNonExistentCompany_ThrowsNotFound()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateAlertService(context);

        var request = new CreateAlertRequestDto
        {
            AlertType = "PRICE_BELOW",
            CompanyId = 9999,
            ThresholdValue = 150.00m
        };

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAlertAsync(request, 1));
    }

    [Fact]
    public async Task CreateAlert_PortfolioAlertForAnotherUser_ThrowsForbidden()
    {
        using var context = CreateInMemoryDbContext();
        int ownerId = 1;
        int attackerId = 2;
        var portfolio = new Portfolio
        {
            PortfolioId = 200,
            UserId = ownerId,
            PortfolioName = "User 1 Portfolio"
        };
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync();

        var service = CreateAlertService(context);

        var request = new CreateAlertRequestDto
        {
            AlertType = "PORTFOLIO_VALUE_ABOVE",
            PortfolioId = 200,
            ThresholdValue = 100000.00m
        };

        var ex = await Assert.ThrowsAsync<AppException>(() => service.CreateAlertAsync(request, attackerId));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task CreateAlert_DuplicateActiveAlert_ThrowsConflict()
    {
        using var context = CreateInMemoryDbContext();
        int userId = 1;
        var company = new Company
        {
            CompanyId = 15,
            TickerSymbol = "SQURPHARMA",
            CompanyName = "Square Pharma",
            CurrentPrice = 210.00m
        };
        context.Companies.Add(company);
        await context.SaveChangesAsync();

        var service = CreateAlertService(context);

        var request = new CreateAlertRequestDto
        {
            AlertType = "PRICE_ABOVE",
            CompanyId = 15,
            ThresholdValue = 250.00m
        };

        await service.CreateAlertAsync(request, userId);

        // Try creating exact same alert again
        var ex = await Assert.ThrowsAsync<AppException>(() => service.CreateAlertAsync(request, userId));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task EvaluatePriceAlerts_PriceCrossesAboveThreshold_TriggersAlertAndDeactivates()
    {
        using var context = CreateInMemoryDbContext();
        int userId = 1;
        var company = new Company
        {
            CompanyId = 1,
            TickerSymbol = "GP",
            CompanyName = "Grameenphone",
            CurrentPrice = 280.00m
        };
        var alert = new Alert
        {
            AlertId = 1,
            UserId = userId,
            CompanyId = 1,
            AlertType = "PRICE_ABOVE",
            ThresholdValue = 275.00m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        context.Companies.Add(company);
        context.Alerts.Add(alert);
        await context.SaveChangesAsync();

        var service = CreateAlertService(context);

        int triggeredCount = await service.EvaluatePriceAlertsAsync();

        Assert.Equal(1, triggeredCount);

        var updatedAlert = await context.Alerts.FindAsync(1);
        Assert.NotNull(updatedAlert);
        Assert.False(updatedAlert.IsActive);
        Assert.NotNull(updatedAlert.TriggeredAt);
        Assert.Contains("280.00", updatedAlert.Message);
        Assert.Contains("275.00", updatedAlert.Message);
    }

    [Fact]
    public async Task EvaluatePriceAlerts_RepeatedTriggerPrevention_DoesNotReTrigger()
    {
        using var context = CreateInMemoryDbContext();
        int userId = 1;
        var company = new Company
        {
            CompanyId = 1,
            TickerSymbol = "GP",
            CompanyName = "Grameenphone",
            CurrentPrice = 300.00m
        };
        var initialTriggeredTime = DateTime.UtcNow.AddMinutes(-30);
        var alert = new Alert
        {
            AlertId = 1,
            UserId = userId,
            CompanyId = 1,
            AlertType = "PRICE_ABOVE",
            ThresholdValue = 275.00m,
            IsActive = false,
            TriggeredAt = initialTriggeredTime,
            Message = "Already triggered earlier"
        };
        context.Companies.Add(company);
        context.Alerts.Add(alert);
        await context.SaveChangesAsync();

        var service = CreateAlertService(context);

        int triggeredCount = await service.EvaluatePriceAlertsAsync();

        // Must NOT trigger again
        Assert.Equal(0, triggeredCount);

        var existingAlert = await context.Alerts.FindAsync(1);
        Assert.NotNull(existingAlert);
        Assert.Equal(initialTriggeredTime, existingAlert.TriggeredAt);
        Assert.Equal("Already triggered earlier", existingAlert.Message);
    }

    [Fact]
    public async Task EvaluatePortfolioAlerts_PortfolioCrossesThreshold_TriggersAlert()
    {
        using var context = CreateInMemoryDbContext();
        int userId = 1;
        var company = new Company
        {
            CompanyId = 10,
            TickerSymbol = "BATBC",
            CompanyName = "BAT Bangladesh",
            CurrentPrice = 500.00m
        };
        var portfolio = new Portfolio
        {
            PortfolioId = 5,
            UserId = userId,
            PortfolioName = "Dividend Portfolio"
        };
        // 100 shares at 500 = 50,000 portfolio value
        var tx = new Transaction
        {
            TransactionId = 1,
            PortfolioId = 5,
            CompanyId = 10,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 450.00m,
            TransactionDate = DateTime.UtcNow.AddDays(-10)
        };
        var alert = new Alert
        {
            AlertId = 20,
            UserId = userId,
            PortfolioId = 5,
            AlertType = "PORTFOLIO_VALUE_ABOVE",
            ThresholdValue = 40000.00m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };

        context.Companies.Add(company);
        context.Portfolios.Add(portfolio);
        context.Transactions.Add(tx);
        context.Alerts.Add(alert);
        await context.SaveChangesAsync();

        var service = CreateAlertService(context);

        int triggeredCount = await service.EvaluatePortfolioAlertsAsync();

        Assert.Equal(1, triggeredCount);

        var updatedAlert = await context.Alerts.FindAsync(20);
        Assert.NotNull(updatedAlert);
        Assert.False(updatedAlert.IsActive);
        Assert.NotNull(updatedAlert.TriggeredAt);
        Assert.Contains("Dividend Portfolio", updatedAlert.Message);
        Assert.Contains("50,000", updatedAlert.Message);
    }

    [Fact]
    public async Task ToggleAlert_ReactivateTriggeredAlert_ResetsTriggerAndActivates()
    {
        using var context = CreateInMemoryDbContext();
        int userId = 1;
        var alert = new Alert
        {
            AlertId = 10,
            UserId = userId,
            CompanyId = 5,
            AlertType = "PRICE_ABOVE",
            ThresholdValue = 300.00m,
            IsActive = false,
            TriggeredAt = DateTime.UtcNow.AddDays(-1),
            Message = "Triggered yesterday"
        };
        context.Alerts.Add(alert);
        await context.SaveChangesAsync();

        var service = CreateAlertService(context);

        var response = await service.ToggleAlertAsync(10, userId);

        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.True(response.Data.IsActive);
        Assert.Null(response.Data.TriggeredAt);
        Assert.Null(response.Data.Message);
        Assert.Equal("ACTIVE", response.Data.Status);
    }

    [Fact]
    public async Task DeleteAlert_BelongingToAnotherUser_ThrowsForbidden()
    {
        using var context = CreateInMemoryDbContext();
        int ownerId = 1;
        int hackerId = 2;
        var alert = new Alert
        {
            AlertId = 55,
            UserId = ownerId,
            CompanyId = 1,
            AlertType = "PRICE_ABOVE",
            ThresholdValue = 100.00m,
            IsActive = true
        };
        context.Alerts.Add(alert);
        await context.SaveChangesAsync();

        var service = CreateAlertService(context);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.DeleteAlertAsync(55, hackerId));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task DeleteAlert_Owner_SuccessfullyDeletes()
    {
        using var context = CreateInMemoryDbContext();
        int ownerId = 1;
        var alert = new Alert
        {
            AlertId = 77,
            UserId = ownerId,
            CompanyId = 1,
            AlertType = "PRICE_ABOVE",
            ThresholdValue = 100.00m,
            IsActive = true
        };
        context.Alerts.Add(alert);
        await context.SaveChangesAsync();

        var service = CreateAlertService(context);

        var response = await service.DeleteAlertAsync(77, ownerId);

        Assert.NotNull(response);
        Assert.True(response.Success);

        var remaining = await context.Alerts.FindAsync(77);
        Assert.Null(remaining);
    }

    [Fact]
    public async Task GetUserAlerts_FiltersByStatusCorrectly()
    {
        using var context = CreateInMemoryDbContext();
        int userId = 1;
        var activeAlert = new Alert
        {
            AlertId = 1,
            UserId = userId,
            AlertType = "PRICE_ABOVE",
            ThresholdValue = 100.00m,
            IsActive = true,
            TriggeredAt = null
        };
        var triggeredAlert = new Alert
        {
            AlertId = 2,
            UserId = userId,
            AlertType = "PRICE_BELOW",
            ThresholdValue = 50.00m,
            IsActive = false,
            TriggeredAt = DateTime.UtcNow.AddHours(-1),
            Message = "Fell below 50.00"
        };
        var disabledAlert = new Alert
        {
            AlertId = 3,
            UserId = userId,
            AlertType = "PORTFOLIO_VALUE_ABOVE",
            ThresholdValue = 200000.00m,
            IsActive = false,
            TriggeredAt = null
        };
        var otherUserAlert = new Alert
        {
            AlertId = 4,
            UserId = 99,
            AlertType = "PRICE_ABOVE",
            ThresholdValue = 120.00m,
            IsActive = true
        };

        context.Alerts.AddRange(activeAlert, triggeredAlert, disabledAlert, otherUserAlert);
        await context.SaveChangesAsync();

        var service = CreateAlertService(context);

        var allRes = await service.GetUserAlertsAsync(userId, "ALL");
        Assert.Equal(3, allRes.Data.Count);

        var activeRes = await service.GetUserAlertsAsync(userId, "ACTIVE");
        Assert.Single(activeRes.Data);
        Assert.Equal(1, activeRes.Data[0].AlertId);

        var trigRes = await service.GetUserAlertsAsync(userId, "TRIGGERED");
        Assert.Single(trigRes.Data);
        Assert.Equal(2, trigRes.Data[0].AlertId);

        var disRes = await service.GetUserAlertsAsync(userId, "DISABLED");
        Assert.Single(disRes.Data);
        Assert.Equal(3, disRes.Data[0].AlertId);
    }
}
