using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.DTOs.Companies;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using ShareSync.Infrastructure.Services;
using Xunit;

namespace ShareSync.Tests;

public class CompanyPriceHistoryTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }

    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ShareSyncDbContext(options);
    }

    [Fact]
    public async Task GetCompanyPriceHistory_WithValidCompany_ReturnsChronologicalPricesAndStats()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var company = new Company
        {
            CompanyId = 1,
            CompanyName = "Grameenphone Ltd.",
            TickerSymbol = "GP",
            SectorId = 1,
            CurrentPrice = 410.50m
        };
        context.Companies.Add(company);

        var now = DateTime.UtcNow;
        context.CompanyPriceHistories.AddRange(
            new CompanyPriceHistory { CompanyId = 1, Price = 390.00m, OpenPrice = 388.00m, HighPrice = 395.00m, LowPrice = 387.00m, Volume = 10000, RecordedAt = now.AddDays(-15) },
            new CompanyPriceHistory { CompanyId = 1, Price = 400.00m, OpenPrice = 392.00m, HighPrice = 405.00m, LowPrice = 390.00m, Volume = 15000, RecordedAt = now.AddDays(-10) },
            new CompanyPriceHistory { CompanyId = 1, Price = 410.50m, OpenPrice = 402.00m, HighPrice = 412.00m, LowPrice = 401.00m, Volume = 20000, RecordedAt = now.AddDays(-1) }
        );
        await context.SaveChangesAsync();

        var service = new CompanyService(context);

        // Act
        var response = await service.GetCompanyPriceHistoryAsync(1);

        // Assert
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(1, response.Data.CompanyId);
        Assert.Equal("GP", response.Data.TickerSymbol);
        Assert.Equal(3, response.Data.TotalPoints);
        Assert.Equal(412.00m, response.Data.PeriodHigh);
        Assert.Equal(387.00m, response.Data.PeriodLow);
        Assert.Equal(20.50m, response.Data.PeriodChange); // 410.50 - 390.00
        Assert.Equal(5.26m, response.Data.PeriodChangePercentage); // (20.50 / 390.00) * 100
        Assert.Equal(3, response.Data.History.Count);
        Assert.True(response.Data.History[0].RecordedAt < response.Data.History[1].RecordedAt);
    }

    [Fact]
    public async Task GetCompanyPriceHistory_WithNonExistentCompany_ThrowsNotFoundException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new CompanyService(context);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetCompanyPriceHistoryAsync(9999));
    }

    [Fact]
    public async Task GetCompanyPriceHistory_WithPeriodFilter_FiltersRecordsCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var company = new Company { CompanyId = 2, CompanyName = "Beximco", TickerSymbol = "BEXIMCO", SectorId = 1, CurrentPrice = 120.00m };
        context.Companies.Add(company);

        var now = DateTime.UtcNow;
        context.CompanyPriceHistories.AddRange(
            new CompanyPriceHistory { CompanyId = 2, Price = 100.00m, RecordedAt = now.AddDays(-40) }, // Outside 1M
            new CompanyPriceHistory { CompanyId = 2, Price = 110.00m, RecordedAt = now.AddDays(-20) }, // Inside 1M
            new CompanyPriceHistory { CompanyId = 2, Price = 120.00m, RecordedAt = now.AddDays(-2) }   // Inside 1M
        );
        await context.SaveChangesAsync();

        var service = new CompanyService(context);

        // Act
        var response = await service.GetCompanyPriceHistoryAsync(2, new CompanyPriceHistoryFilterDto { Period = "1M" });

        // Assert
        Assert.True(response.Success);
        Assert.Equal(2, response.Data!.TotalPoints);
        Assert.Equal(110.00m, response.Data.History.First().Price);
        Assert.Equal(120.00m, response.Data.History.Last().Price);
    }

    [Fact]
    public async Task SyncAllCompanyPricesAsync_InsertsPriceHistoryRecordAndUpdatesCurrentPrice()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        context.Companies.Add(new Company
        {
            CompanyId = 3,
            TickerSymbol = "SQURPHARMA",
            CompanyName = "Square Pharma",
            SectorId = 1,
            CurrentPrice = 220.00m
        });
        await context.SaveChangesAsync();

        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                quote = new
                {
                    symbol = "SQURPHARMA",
                    close = 230.50m,
                    open = 222.00m,
                    high = 232.00m,
                    low = 221.50m,
                    volume = 55000L
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var service = new DsePriceService(context, client, NullLogger<DsePriceService>.Instance);

        // Act
        var response = await service.SyncAllCompanyPricesAsync();

        // Assert
        Assert.True(response.Success);
        Assert.Equal(1, response.Data!.UpdatedCount);
        Assert.Equal(1, response.Data.HistoryCount);

        var company = await context.Companies.FirstAsync(c => c.CompanyId == 3);
        Assert.Equal(230.50m, company.CurrentPrice);

        var history = await context.CompanyPriceHistories.Where(h => h.CompanyId == 3).ToListAsync();
        Assert.Single(history);
        Assert.Equal(230.50m, history[0].Price);
        Assert.Equal(222.00m, history[0].OpenPrice);
        Assert.Equal(232.00m, history[0].HighPrice);
        Assert.Equal(221.50m, history[0].LowPrice);
        Assert.Equal(55000L, history[0].Volume);
    }

    [Fact]
    public async Task SyncAllCompanyPricesAsync_DuplicatePrevention_DoesNotInsertDuplicateWithinSyncInterval()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var company = new Company
        {
            CompanyId = 4,
            TickerSymbol = "BATBC",
            CompanyName = "BATBC",
            SectorId = 1,
            CurrentPrice = 460.00m
        };
        context.Companies.Add(company);

        // Existing snapshot recorded 2 minutes ago with price 460.00
        var existingHistory = new CompanyPriceHistory
        {
            CompanyId = 4,
            Price = 460.00m,
            RecordedAt = DateTime.UtcNow.AddMinutes(-2),
            CreatedAt = DateTime.UtcNow.AddMinutes(-2)
        };
        context.CompanyPriceHistories.Add(existingHistory);
        await context.SaveChangesAsync();

        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                quote = new
                {
                    symbol = "BATBC",
                    close = 460.00m // Same price
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var service = new DsePriceService(context, client, NullLogger<DsePriceService>.Instance);

        // Act
        var response = await service.SyncAllCompanyPricesAsync();

        // Assert
        Assert.True(response.Success);
        Assert.Equal(0, response.Data!.HistoryCount); // Skipped because within 10-minute duplicate window

        var totalHistory = await context.CompanyPriceHistories.CountAsync(h => h.CompanyId == 4);
        Assert.Equal(1, totalHistory); // Still only the original record
    }

    [Fact]
    public async Task SyncAllCompanyPricesAsync_WhenQuoteHasInvalidPrice_RejectsAndPreservesExistingPrice()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        context.Companies.Add(new Company
        {
            CompanyId = 5,
            TickerSymbol = "GP",
            CompanyName = "GP",
            SectorId = 1,
            CurrentPrice = 410.00m
        });
        await context.SaveChangesAsync();

        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                quote = new
                {
                    symbol = "GP",
                    close = -50.00m // Invalid negative price!
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var service = new DsePriceService(context, client, NullLogger<DsePriceService>.Instance);

        // Act
        var response = await service.SyncAllCompanyPricesAsync();

        // Assert
        Assert.True(response.Success);
        Assert.Equal(0, response.Data!.UpdatedCount);
        Assert.Equal(0, response.Data.HistoryCount);

        var company = await context.Companies.FirstAsync(c => c.CompanyId == 5);
        Assert.Equal(410.00m, company.CurrentPrice); // Untouched

        var historyCount = await context.CompanyPriceHistories.CountAsync(h => h.CompanyId == 5);
        Assert.Equal(0, historyCount); // No invalid history inserted
    }
}
