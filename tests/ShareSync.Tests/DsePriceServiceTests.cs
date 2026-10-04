using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using ShareSync.Infrastructure.Services;
using Xunit;

namespace ShareSync.Tests;

public class DsePriceServiceTests
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
    public async Task FetchLatestPriceAsync_WithValidClosePrice_ReturnsParsedPrice()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                quote = new
                {
                    symbol = "GP",
                    close = 240.80m,
                    open = 239.50m
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
        var price = await service.FetchLatestPriceAsync("GP");

        // Assert
        Assert.NotNull(price);
        Assert.Equal(240.80m, price.Value);
    }

    [Fact]
    public async Task FetchLatestPriceAsync_WhenHttpError_ReturnsNullGracefully()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        using var client = new HttpClient(handler);
        var service = new DsePriceService(context, client, NullLogger<DsePriceService>.Instance);

        // Act
        var price = await service.FetchLatestPriceAsync("BATBC");

        // Assert
        Assert.Null(price);
    }

    [Fact]
    public async Task SyncAllCompanyPricesAsync_UpdatesCompanyCurrentPriceInDb()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        context.Companies.Add(new Company
        {
            CompanyId = 1,
            TickerSymbol = "GP",
            CompanyName = "Grameenphone Ltd.",
            SectorId = 1,
            CurrentPrice = 200.00m
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
                    close = 245.50m
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
        Assert.Equal(1, response.Data.UpdatedCount);
        Assert.Equal(245.50m, response.Data.UpdatedPrices["GP"]);

        var updatedCompany = await context.Companies.FirstAsync(c => c.TickerSymbol == "GP");
        Assert.Equal(245.50m, updatedCompany.CurrentPrice);
    }

    [Fact]
    public async Task SyncAllCompanyPricesAsync_WhenNoCompanies_ReturnsZeroUpdated()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler);
        var service = new DsePriceService(context, client, NullLogger<DsePriceService>.Instance);

        // Act
        var response = await service.SyncAllCompanyPricesAsync();

        // Assert
        Assert.True(response.Success);
        Assert.Equal(0, response.Data.UpdatedCount);
    }
}
