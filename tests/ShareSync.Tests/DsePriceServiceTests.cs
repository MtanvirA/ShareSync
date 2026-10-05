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

    [Fact]
    public async Task GetDseListedCompaniesAsync_ReturnsListedCompaniesWithTrackingStatus()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var sector = new Sector { SectorId = 1, SectorName = "Telecommunication" };
        context.Sectors.Add(sector);
        context.Companies.Add(new Company
        {
            CompanyId = 10,
            TickerSymbol = "GP",
            CompanyName = "Grameenphone Ltd.",
            SectorId = 1,
            CurrentPrice = 350.00m,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                companies = new[]
                {
                    new { symbol = "GP", name = "Grameenphone Ltd.", sector = "Telecommunication", category = "A", market_cap_mn = 386000m },
                    new { symbol = "BATBC", name = "British American Tobacco", sector = "Food & Allied", category = "A", market_cap_mn = 280000m }
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
        var list = await service.GetDseListedCompaniesAsync();

        // Assert
        Assert.NotEmpty(list);
        var gp = list.First(c => c.Symbol == "GP");
        Assert.True(gp.IsAdded);
        Assert.Equal(10, gp.LocalCompanyId);
        Assert.Equal(350.00m, gp.LocalPrice);

        var batbc = list.First(c => c.Symbol == "BATBC");
        Assert.False(batbc.IsAdded);
        Assert.Null(batbc.LocalCompanyId);
    }

    [Fact]
    public async Task ImportCompanyFromDseAsync_WhenNotExists_CreatesCompanyAndReturnsDto()
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
                    symbol = "RENATA",
                    close = 450.00m,
                    open = 445.00m,
                    high = 455.00m,
                    low = 442.00m,
                    volume = 25000
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
        var result = await service.ImportCompanyFromDseAsync("RENATA", "Renata Limited", "Pharmaceuticals");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("RENATA", result.TickerSymbol);
        Assert.Equal("Renata Limited", result.CompanyName);
        Assert.Equal("Pharmaceuticals", result.SectorName);
        Assert.Equal(450.00m, result.CurrentPrice);

        var dbCompany = await context.Companies.Include(c => c.Sector).FirstOrDefaultAsync(c => c.TickerSymbol == "RENATA");
        Assert.NotNull(dbCompany);
        Assert.Equal(450.00m, dbCompany.CurrentPrice);
        Assert.Equal("Pharmaceuticals", dbCompany.Sector.SectorName);

        var history = await context.CompanyPriceHistories.FirstOrDefaultAsync(h => h.CompanyId == dbCompany.CompanyId);
        Assert.NotNull(history);
        Assert.Equal(450.00m, history.Price);
    }

    [Fact]
    public async Task ImportCompanyFromDseAsync_WhenAlreadyExists_ReturnsExistingWithoutDuplication()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var sector = new Sector { SectorId = 5, SectorName = "Pharmaceuticals" };
        context.Sectors.Add(sector);
        context.Companies.Add(new Company
        {
            CompanyId = 50,
            TickerSymbol = "SQURPHARMA",
            CompanyName = "Square Pharmaceuticals",
            SectorId = 5,
            CurrentPrice = 220.00m,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler);
        var service = new DsePriceService(context, client, NullLogger<DsePriceService>.Instance);

        // Act
        var result = await service.ImportCompanyFromDseAsync("SQURPHARMA");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(50, result.CompanyId);
        Assert.Equal("SQURPHARMA", result.TickerSymbol);
        Assert.Equal(220.00m, result.CurrentPrice);

        var count = await context.Companies.CountAsync(c => c.TickerSymbol == "SQURPHARMA");
        Assert.Equal(1, count);
    }
}
