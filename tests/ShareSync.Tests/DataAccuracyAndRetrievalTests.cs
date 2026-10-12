using System;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShareSync.Application.Common.Helpers;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using ShareSync.Infrastructure.Services;
using Xunit;

namespace ShareSync.Tests;

public class DataAccuracyAndRetrievalTests
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

    // 1. Valid market-data record
    [Fact]
    public async Task FetchQuoteDetailsAsync_ValidMarketDataRecord_ParsesAllFieldsAndHarmonizesOhlc()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                quote = new
                {
                    symbol = "GP",
                    open = 240.00m,
                    high = 245.50m,
                    low = 239.00m,
                    close = 244.20m,
                    volume = 150000L
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var service = new DsePriceService(context, client, NullLogger<DsePriceService>.Instance);

        var quote = await service.FetchQuoteDetailsAsync("GP");

        Assert.NotNull(quote);
        Assert.Equal(244.20m, quote.Price);
        Assert.Equal(240.00m, quote.OpenPrice);
        Assert.Equal(245.50m, quote.HighPrice);
        Assert.Equal(239.00m, quote.LowPrice);
        Assert.Equal(150000L, quote.Volume);
    }

    // 2. Malformed response
    [Fact]
    public async Task FetchQuoteDetailsAsync_MalformedResponse_ReturnsNullSafely()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ not valid json at all", System.Text.Encoding.UTF8, "application/json")
        });

        using var client = new HttpClient(handler);
        var service = new DsePriceService(context, client, NullLogger<DsePriceService>.Instance);

        var quote = await service.FetchQuoteDetailsAsync("BATBC");

        Assert.Null(quote);
    }

    // 3. Missing price
    [Fact]
    public async Task FetchQuoteDetailsAsync_MissingPrice_ReturnsNullSafely()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                quote = new
                {
                    symbol = "SQUAREPHARMA",
                    volume = 5000L
                    // close is completely absent
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var service = new DsePriceService(context, client, NullLogger<DsePriceService>.Instance);

        var quote = await service.FetchQuoteDetailsAsync("SQUAREPHARMA");

        Assert.Null(quote);
    }

    // 4. Invalid price (negative or extreme out-of-bounds)
    [Fact]
    public async Task FetchQuoteDetailsAsync_NegativeOrImpossiblePrice_Rejected()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                quote = new
                {
                    symbol = "GP",
                    close = -50.00m
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var service = new DsePriceService(context, client, NullLogger<DsePriceService>.Instance);

        var quote = await service.FetchQuoteDetailsAsync("GP");

        Assert.Null(quote);
        Assert.False(MarketDataPathResolver.IsValidPrice(-50.00m));
        Assert.False(MarketDataPathResolver.IsValidPrice(9999999m));
    }

    // 5. Duplicate record (idempotent daily snapshot)
    [Fact]
    public async Task SyncAllCompanyPricesAsync_DailyDuplicate_UpdatesExistingRecordWithoutInsertingNewRow()
    {
        using var context = CreateInMemoryDbContext();
        var company = new Company
        {
            CompanyId = 1,
            TickerSymbol = "GP",
            CompanyName = "Grameenphone Ltd.",
            CurrentPrice = 240.00m,
            IsActive = true
        };
        context.Companies.Add(company);

        var today = DateTime.UtcNow.Date;
        var existingHistory = new CompanyPriceHistory
        {
            CompanyId = 1,
            TradingDate = today,
            Price = 240.00m,
            RecordedAt = DateTime.UtcNow.AddMinutes(-30),
            CreatedAt = DateTime.UtcNow.AddMinutes(-30)
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
                    symbol = "GP",
                    close = 242.00m,
                    high = 243.00m
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var service = new DsePriceService(context, client, NullLogger<DsePriceService>.Instance);

        var response = await service.SyncAllCompanyPricesAsync();

        Assert.True(response.Success);
        Assert.Equal(0, response.Data!.HistoryCount); // No new history row created
        Assert.Equal(1, response.Data!.UpdatedCount); // Current price updated

        var totalRows = await context.CompanyPriceHistories.CountAsync(h => h.CompanyId == 1);
        Assert.Equal(1, totalRows); // Idempotent: exactly 1 record retained

        var updatedHistory = await context.CompanyPriceHistories.FirstAsync(h => h.CompanyId == 1);
        Assert.Equal(242.00m, updatedHistory.Price);
    }

    // 6. Conflicting duplicate (extreme flash price deviation rejected)
    [Fact]
    public async Task SyncAllCompanyPricesAsync_ExtremeDeviation_ProtectsCurrentPrice()
    {
        using var context = CreateInMemoryDbContext();
        var company = new Company
        {
            CompanyId = 2,
            TickerSymbol = "BATBC",
            CompanyName = "British American Tobacco",
            CurrentPrice = 450.00m,
            IsActive = true
        };
        context.Companies.Add(company);
        await context.SaveChangesAsync();

        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                quote = new
                {
                    symbol = "BATBC",
                    close = 50000.00m // 111x flash surge anomaly
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var service = new DsePriceService(context, client, NullLogger<DsePriceService>.Instance);

        var response = await service.SyncAllCompanyPricesAsync();

        Assert.True(response.Success);
        Assert.Equal(0, response.Data!.UpdatedCount); // Rejected extreme deviation
        Assert.Equal(450.00m, company.CurrentPrice); // Original price preserved
    }

    // 7. Ticker normalization
    [Theory]
    [InlineData("gp", "GP")]
    [InlineData("  squarepharma  ", "SQUAREPHARMA")]
    [InlineData("cash-bdt", "CASH-BDT")]
    [InlineData("bracbank\t\n", "BRACBANK")]
    public void MarketDataPathResolver_NormalizeTicker_ReturnsCanonicalRepresentation(string raw, string expected)
    {
        var normalized = MarketDataPathResolver.NormalizeTicker(raw);
        Assert.Equal(expected, normalized);
    }

    // 8. API timeout
    [Fact]
    public async Task FetchQuoteDetailsAsync_WhenTimeoutOccurs_GracefullyReturnsNull()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new MockHttpMessageHandler(req =>
        {
            throw new TaskCanceledException("HttpClient timeout");
        });

        using var client = new HttpClient(handler);
        var service = new DsePriceService(context, client, NullLogger<DsePriceService>.Instance);

        var quote = await service.FetchQuoteDetailsAsync("GP");

        Assert.Null(quote);
    }

    // 9. API failure (HTTP 503 Service Unavailable)
    [Fact]
    public async Task FetchQuoteDetailsAsync_Http503_ReturnsNullGracefully()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        using var client = new HttpClient(handler);
        var service = new DsePriceService(context, client, NullLogger<DsePriceService>.Instance);

        var quote = await service.FetchQuoteDetailsAsync("RENATA");

        Assert.Null(quote);
    }

    // 10. Retry behavior (first try 500, second try succeeds)
    [Fact]
    public async Task FetchQuoteDetailsAsync_RetryOnTransient500_SucceedsOnSecondAttempt()
    {
        using var context = CreateInMemoryDbContext();
        int attempts = 0;
        var handler = new MockHttpMessageHandler(req =>
        {
            attempts++;
            if (attempts == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                quote = new
                {
                    symbol = "GP",
                    close = 241.50m
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var service = new DsePriceService(context, client, NullLogger<DsePriceService>.Instance);

        var quote = await service.FetchQuoteDetailsAsync("GP");

        Assert.NotNull(quote);
        Assert.Equal(241.50m, quote.Price);
        Assert.Equal(2, attempts); // Verified 2 attempts made
    }

    // 11. Idempotent import & date parsing
    [Theory]
    [InlineData("2025-04-08", 2025, 4, 8)]
    [InlineData("2024-12-31", 2024, 12, 31)]
    [InlineData("2023-01-05", 2023, 1, 5)]
    public void MarketDataPathResolver_TryParseTradingDate_NeverInvertsMonthAndDay(string rawDate, int year, int month, int day)
    {
        bool success = MarketDataPathResolver.TryParseTradingDate(rawDate, out DateTime parsed);
        Assert.True(success);
        Assert.Equal(year, parsed.Year);
        Assert.Equal(month, parsed.Month);
        Assert.Equal(day, parsed.Day);
    }

    // 12. Source provenance
    [Fact]
    public async Task SyncAllCompanyPricesAsync_PopulatesCompleteProvenanceMetadata()
    {
        using var context = CreateInMemoryDbContext();
        var company = new Company
        {
            CompanyId = 3,
            TickerSymbol = "LHBL",
            CompanyName = "LafargeHolcim",
            CurrentPrice = 70.00m,
            IsActive = true
        };
        context.Companies.Add(company);
        await context.SaveChangesAsync();

        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                quote = new
                {
                    symbol = "LHBL",
                    close = 71.50m,
                    open = 70.20m,
                    high = 72.00m,
                    low = 70.00m,
                    volume = 80000L
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var service = new DsePriceService(context, client, NullLogger<DsePriceService>.Instance);

        await service.SyncAllCompanyPricesAsync();

        var history = await context.CompanyPriceHistories.FirstOrDefaultAsync(h => h.CompanyId == 3);
        Assert.NotNull(history);
        Assert.Equal("StockChartBD Live API", history.Source);
        Assert.Equal("DSE Real-Time Market Feed", history.SourceDataset);
        Assert.NotNull(history.ImportBatchId);
        Assert.StartsWith("SYNC_", history.ImportBatchId);
        Assert.Equal(DateTime.UtcNow.Date, history.TradingDate);
    }

    // 13. Stale-record protection
    [Fact]
    public async Task SyncAllCompanyPricesAsync_WhenApiReturnsNull_PreservesExistingPrice()
    {
        using var context = CreateInMemoryDbContext();
        var company = new Company
        {
            CompanyId = 4,
            TickerSymbol = "WALTONHIL",
            CompanyName = "Walton Hi-Tech",
            CurrentPrice = 690.00m,
            IsActive = true
        };
        context.Companies.Add(company);
        await context.SaveChangesAsync();

        // API fails completely
        var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.BadGateway));

        using var client = new HttpClient(handler);
        var service = new DsePriceService(context, client, NullLogger<DsePriceService>.Instance);

        var result = await service.SyncAllCompanyPricesAsync();

        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.UpdatedCount);
        Assert.Equal(690.00m, company.CurrentPrice); // Stale price protected from wipeout
        Assert.Equal(690.00m, result.Data.UpdatedPrices["WALTONHIL"]);
    }
}
