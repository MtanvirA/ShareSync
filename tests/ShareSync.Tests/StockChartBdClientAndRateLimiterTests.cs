using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.MarketData;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using ShareSync.Infrastructure.Services;
using Xunit;

namespace ShareSync.Tests;

public class StockChartBdClientAndRateLimiterTests
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

    private (StockChartBdClient Client, StockChartBdRateLimiter Limiter) CreateTestClient(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        StockChartBdOptions? customOptions = null)
    {
        StockChartBdClient.ResetCaches();
        DsePriceService.ResetCaches();

        var options = customOptions ?? new StockChartBdOptions
        {
            BaseUrl = "https://stockchartbd.com/api/v1.php",
            MaxRequestsPerHour = 100,
            MarketSummaryCacheTtlMinutes = 5,
            CatalogCacheTtlMinutes = 60,
            TimeoutSeconds = 10
        };

        var optionsWrapper = Options.Create(options);
        var limiter = new StockChartBdRateLimiter(optionsWrapper, NullLogger<StockChartBdRateLimiter>.Instance);
        var httpClient = new HttpClient(new MockHttpMessageHandler(responder));
        var client = new StockChartBdClient(httpClient, limiter, optionsWrapper, NullLogger<StockChartBdClient>.Instance);

        return (client, limiter);
    }

    // 1. All 7 Endpoints Parsing and Contract Verification

    [Fact]
    public async Task GetQuoteAsync_ValidContract_ParsesAllFieldsCorrectly()
    {
        var (client, _) = CreateTestClient(req =>
        {
            Assert.Contains("endpoint=quote", req.RequestUri!.Query);
            Assert.Contains("symbol=GP", req.RequestUri.Query);

            var json = """
            {
                "ok": true,
                "symbol": "GP",
                "quote": {
                    "date": "2026-10-11",
                    "close": 240.80,
                    "open": 239.50,
                    "high": 242.00,
                    "low": 239.00,
                    "previous_close": 240.00,
                    "change": 0.80,
                    "change_percent": 0.33,
                    "volume": 1250000,
                    "trades": 450,
                    "value_mn": 300.5
                },
                "meta": {
                    "as_of": "2026-10-11 14:30:00"
                }
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var quote = await client.GetQuoteAsync("GP");

        Assert.NotNull(quote);
        Assert.Equal(240.80m, quote.Price);
        Assert.Equal(239.50m, quote.OpenPrice);
        Assert.Equal(242.00m, quote.HighPrice);
        Assert.Equal(239.00m, quote.LowPrice);
        Assert.Equal(1250000, quote.Volume);
        Assert.Equal("StockChartBD Live API", quote.Source);
        Assert.Equal("LIVE", quote.Status);
    }

    [Fact]
    public async Task GetMarketSummaryAsync_ValidContract_ParsesIndicesAndBreadth()
    {
        var (client, _) = CreateTestClient(req =>
        {
            Assert.Contains("endpoint=market", req.RequestUri!.Query);
            var json = """
            {
                "ok": true,
                "indices": {
                    "DSEX": { "close": 5345.50, "change": 12.30, "change_percent": 0.23, "date": "2026-10-11" },
                    "DS30": { "close": 1940.20, "change": -4.10, "change_percent": -0.21, "date": "2026-10-11" },
                    "DSES": { "close": 1180.10, "change": 1.50, "change_percent": 0.13, "date": "2026-10-11" },
                    "CDSET": { "close": 1250.00, "change": 5.00, "change_percent": 0.40, "date": "2026-10-11" }
                },
                "breadth": {
                    "date": "2026-10-11",
                    "advanced": 180,
                    "declined": 140,
                    "unchanged": 75,
                    "total_volume": 120000000,
                    "total_value_mn": 4500.0,
                    "total_trades": 154000
                },
                "meta": {
                    "as_of": "2026-10-11 14:30:00"
                }
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var summary = await client.GetMarketSummaryAsync();

        Assert.NotNull(summary);
        Assert.True(summary.Ok);
        Assert.Equal(4, summary.Indices.Count);
        Assert.NotNull(summary.Breadth);
        Assert.Equal(180, summary.Breadth.Advanced);
        Assert.Equal(140, summary.Breadth.Declined);
        Assert.Equal(75, summary.Breadth.Unchanged);
    }

    [Fact]
    public async Task GetCompaniesAsync_ValidContract_ParsesCatalog()
    {
        var (client, _) = CreateTestClient(req =>
        {
            Assert.Contains("endpoint=companies", req.RequestUri!.Query);
            var json = """
            {
                "ok": true,
                "count": 2,
                "companies": [
                    { "symbol": "GP", "name": "Grameenphone Ltd.", "sector": "Telecommunication", "category": "A", "market_cap_mn": 325143.0 },
                    { "symbol": "BATBC", "name": "British American Tobacco Bangladesh", "sector": "Food & Allied", "category": "A", "market_cap_mn": 280000.0 }
                ]
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var companies = await client.GetCompaniesAsync();

        Assert.NotNull(companies);
        Assert.Equal(2, companies.Count);
        Assert.Contains(companies, c => c.Symbol == "GP" && c.Sector == "Telecommunication");
        Assert.Contains(companies, c => c.Symbol == "BATBC" && c.Sector == "Food & Allied");
    }

    [Fact]
    public async Task GetHistoricalPricesAsync_ValidContract_ParsesHistoricalRecords()
    {
        var (client, _) = CreateTestClient(req =>
        {
            Assert.Contains("endpoint=history", req.RequestUri!.Query);
            Assert.Contains("symbol=GP", req.RequestUri.Query);
            Assert.Contains("limit=5", req.RequestUri.Query);

            var json = """
            {
                "ok": true,
                "symbol": "GP",
                "count": 2,
                "history": [
                    { "date": "2026-10-11", "open": 239.5, "high": 242.0, "low": 239.0, "close": 240.8, "volume": 1250000, "trades": 450, "value_mn": 300.5 },
                    { "date": "2026-10-10", "open": 240.0, "high": 241.0, "low": 238.5, "close": 239.5, "volume": 980000, "trades": 380, "value_mn": 235.0 }
                ]
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var history = await client.GetHistoricalPricesAsync("GP", 5);

        Assert.NotNull(history);
        Assert.Equal(2, history.Count);
        Assert.Equal("2026-10-11", history[0].Date);
        Assert.Equal(240.8m, history[0].Close);
    }

    [Fact]
    public async Task GetFinancialsAsync_ValidContract_ParsesFinancialMetrics()
    {
        var (client, _) = CreateTestClient(req =>
        {
            Assert.Contains("endpoint=financials", req.RequestUri!.Query);
            Assert.Contains("symbol=GP", req.RequestUri.Query);

            var json = """
            {
                "ok": true,
                "symbol": "GP",
                "yearly": [
                    { "year": "2024", "eps": 24.50, "nav_per_share": 42.10, "pe_at_year_end": 9.8, "dividend_yield_percent": 7.5 }
                ]
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var fin = await client.GetFinancialsAsync("GP");

        Assert.NotNull(fin);
        Assert.True(fin.Ok);
        Assert.NotEmpty(fin.Yearly);
        Assert.Equal(24.50m, fin.Yearly[0].Eps);
        Assert.Equal(9.8m, fin.Yearly[0].PeAtYearEnd);
        Assert.Equal(42.10m, fin.Yearly[0].NavPerShare);
    }

    [Fact]
    public async Task GetHealthAsync_ValidContract_ParsesHealthMetrics()
    {
        var (client, _) = CreateTestClient(req =>
        {
            Assert.Contains("endpoint=health", req.RequestUri!.Query);
            Assert.Contains("symbol=GP", req.RequestUri.Query);

            var json = """
            {
                "ok": true,
                "health": {
                    "symbol": "GP",
                    "score": 82,
                    "band": "A",
                    "category": "A-Category"
                }
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var health = await client.GetHealthAsync("GP");

        Assert.NotNull(health);
        Assert.True(health.Ok);
        Assert.NotNull(health.Health);
        Assert.Equal(82, health.Health.Score);
        Assert.Equal("A", health.Health.Band);
    }

    [Fact]
    public async Task GetSectorsAsync_ValidContract_ParsesSectorsList()
    {
        var (client, _) = CreateTestClient(req =>
        {
            Assert.Contains("endpoint=sectors", req.RequestUri!.Query);

            var json = """
            {
                "ok": true,
                "count": 2,
                "sectors": [
                    { "sector": "Pharmaceuticals & Chemicals", "companies": 34, "market_cap_mn": 680000.0 },
                    { "sector": "Bank", "companies": 36, "market_cap_mn": 520000.0 }
                ]
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var sectors = await client.GetSectorsAsync();

        Assert.NotNull(sectors);
        Assert.Equal(2, sectors.Count);
        Assert.Equal("Pharmaceuticals & Chemicals", sectors[0].Sector);
    }

    // 2. Malformed, Non-JSON, and HTML Error Handling

    [Fact]
    public async Task GetQuoteAsync_HtmlErrorResponse_GracefullyReturnsNull()
    {
        var (client, _) = CreateTestClient(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("<html><body>502 Bad Gateway</body></html>", System.Text.Encoding.UTF8, "text/html")
            };
        });

        var quote = await client.GetQuoteAsync("GP");
        Assert.Null(quote);
    }

    [Fact]
    public async Task GetQuoteAsync_MalformedJson_GracefullyReturnsNull()
    {
        var (client, _) = CreateTestClient(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\": true, quote: {broken", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var quote = await client.GetQuoteAsync("GP");
        Assert.Null(quote);
    }

    [Fact]
    public async Task GetQuoteAsync_OkFalseInPayload_ReturnsNull()
    {
        var (client, _) = CreateTestClient(req =>
        {
            var json = "{\"ok\": false, \"error\": \"No price on record for INVALID\"}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var quote = await client.GetQuoteAsync("INVALID");
        Assert.Null(quote);
    }

    [Fact]
    public async Task GetQuoteAsync_ZeroOrNegativePrice_ReturnsNull()
    {
        var (client, _) = CreateTestClient(req =>
        {
            var json = """
            {
                "ok": true,
                "quote": {
                    "close": 0.0,
                    "open": 0.0
                }
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var quote = await client.GetQuoteAsync("GP");
        Assert.Null(quote);
    }

    // 3. Rate Limiter, HTTP 429, and Rolling Budget Enforcement

    [Fact]
    public void RateLimiter_EnforcesHourlyBudget_RejectsWhenLimitExceeded()
    {
        var options = Options.Create(new StockChartBdOptions
        {
            MaxRequestsPerHour = 5
        });
        var limiter = new StockChartBdRateLimiter(options, NullLogger<StockChartBdRateLimiter>.Instance);

        // 5 requests allowed
        for (int i = 0; i < 5; i++)
        {
            Assert.True(limiter.CanAcquireSlot(out _));
            limiter.RecordRequest();
        }

        // 6th request rejected
        Assert.False(limiter.CanAcquireSlot(out var waitTime));
        Assert.True(waitTime > TimeSpan.Zero);
        Assert.Equal(0, limiter.GetRemainingBudget());
    }

    [Fact]
    public void RateLimiter_OnHttp429WithRetryAfter_EnforcesCooldown()
    {
        var options = Options.Create(new StockChartBdOptions
        {
            MaxRequestsPerHour = 100
        });
        var limiter = new StockChartBdRateLimiter(options, NullLogger<StockChartBdRateLimiter>.Instance);

        Assert.True(limiter.CanAcquireSlot(out _));

        // Server reports 429 with 60s Retry-After
        limiter.Record429(TimeSpan.FromSeconds(60));

        // Subsequent budget requests are immediately blocked
        Assert.False(limiter.CanAcquireSlot(out _));
        Assert.True(limiter.IsThrottled);
    }

    [Fact]
    public async Task Client_OnHttp429Response_ReportsRateLimitEventAndReturnsNull()
    {
        int callCount = 0;
        var (client, limiter) = CreateTestClient(req =>
        {
            callCount++;
            var response = new HttpResponseMessage((HttpStatusCode)429);
            response.Headers.TryAddWithoutValidation("Retry-After", "120");
            return response;
        });

        var quote1 = await client.GetQuoteAsync("GP");
        Assert.Null(quote1);
        Assert.Equal(1, callCount);

        // Second call is immediately throttled by rate limiter without consuming network
        var quote2 = await client.GetQuoteAsync("BATBC");
        Assert.Null(quote2);
        Assert.Equal(1, callCount); // No second HTTP request was fired!
    }

    // 4. Caching and Last-Known-Good Retention

    [Fact]
    public async Task Client_CachesSuccessfulResponse_DoesNotDuplicateHttpCalls()
    {
        int networkCalls = 0;
        var (client, _) = CreateTestClient(req =>
        {
            networkCalls++;
            var json = """
            {
                "ok": true,
                "quote": {
                    "close": 240.80,
                    "open": 239.50
                }
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var quote1 = await client.GetQuoteAsync("GP");
        var quote2 = await client.GetQuoteAsync("GP");

        Assert.NotNull(quote1);
        Assert.NotNull(quote2);
        Assert.Equal(1, networkCalls); // Served from in-memory cache!
    }

    [Fact]
    public async Task GetCompaniesAsync_WhenSubsequentRefreshFails_RetainsLastKnownGoodCatalog()
    {
        int callCount = 0;
        var (client, _) = CreateTestClient(req =>
        {
            callCount++;
            if (callCount == 1)
            {
                var json = """
                {
                    "ok": true,
                    "count": 1,
                    "companies": [
                        { "symbol": "GP", "name": "Grameenphone Ltd.", "sector": "Telecommunication" }
                    ]
                }
                """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });

        // 1st call: succeeds
        var catalog1 = await client.GetCompaniesAsync(forceRefresh: false);
        Assert.Single(catalog1!);

        // 2nd call with forceRefresh: upstream fails with 503, but last-known-good catalog is retained!
        var catalog2 = await client.GetCompaniesAsync(forceRefresh: true);
        Assert.NotNull(catalog2);
        Assert.Single(catalog2);
        Assert.Equal("GP", catalog2[0].Symbol);
    }

    [Fact]
    public void Client_Diagnostics_ReportsBudgetAndCacheMetrics()
    {
        var (client, limiter) = CreateTestClient(req => new HttpResponseMessage(HttpStatusCode.OK));
        limiter.RecordRequest();

        var diagnostics = client.GetDiagnostics();

        Assert.NotNull(diagnostics);
        Assert.Equal(100, diagnostics.HourlyBudget);
        Assert.Equal(1, diagnostics.RequestsUsedInLastHour);
        Assert.Equal(99, diagnostics.RemainingHourlyBudget);
    }

    // 5. Symbol Alias Resolution

    [Fact]
    public async Task Client_NormalizesAliases_ResolvesSquarePharma()
    {
        string? requestedSymbol = null;
        var (client, _) = CreateTestClient(req =>
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.RequestUri!.Query);
            requestedSymbol = query["symbol"];

            var json = """
            {
                "ok": true,
                "symbol": "SQURPHARMA",
                "quote": {
                    "close": 225.50,
                    "open": 224.00
                }
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        // Database canonical ticker SQUAREPHARMA
        var quote = await client.GetQuoteAsync("SQUAREPHARMA");

        Assert.NotNull(quote);
        Assert.Equal("SQURPHARMA", requestedSymbol); // StockChartBD API symbol was correctly requested
        Assert.Equal(225.50m, quote.Price);
    }

    // 6. Persistence and Financial Integrity Preservation in DsePriceService

    [Fact]
    public async Task DsePriceService_RejectsExtremePriceAnomaly_PreservesStoredPrice()
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
        await context.SaveChangesAsync();

        // Upstream returns an extreme implausible price anomaly (e.g., 3000.00m, 12.5x spike)
        var (client, _) = CreateTestClient(req =>
        {
            var json = """
            {
                "ok": true,
                "quote": {
                    "close": 3000.00,
                    "open": 3000.00
                }
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var priceService = new DsePriceService(
            context,
            new HttpClient(new MockHttpMessageHandler(r => new HttpResponseMessage(HttpStatusCode.BadRequest))),
            client,
            null,
            NullLogger<DsePriceService>.Instance);

        var result = await priceService.SyncAllCompanyPricesAsync();

        var refreshedCompany = await context.Companies.FindAsync(1);
        Assert.NotNull(refreshedCompany);
        // Valid stored price 240.00m is preserved; anomalous 2400.00m was rejected!
        Assert.Equal(240.00m, refreshedCompany.CurrentPrice);
    }

    [Fact]
    public async Task DsePriceService_IdempotentSameSessionSync_DoesNotDuplicatePriceHistory()
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
        await context.SaveChangesAsync();

        var (client, _) = CreateTestClient(req =>
        {
            var json = """
            {
                "ok": true,
                "quote": {
                    "close": 241.50,
                    "open": 240.00,
                    "high": 242.00,
                    "low": 239.50,
                    "volume": 500000
                }
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var priceService = new DsePriceService(
            context,
            new HttpClient(new MockHttpMessageHandler(r => new HttpResponseMessage(HttpStatusCode.BadRequest))),
            client,
            null,
            NullLogger<DsePriceService>.Instance);

        // Run sync twice in the same session
        await priceService.SyncAllCompanyPricesAsync();
        await priceService.SyncAllCompanyPricesAsync();

        // History record should exist but not be duplicated
        var historyCount = await context.CompanyPriceHistories.CountAsync(h => h.CompanyId == 1);
        Assert.Equal(1, historyCount);

        var companyInDb = await context.Companies.FindAsync(1);
        Assert.Equal(241.50m, companyInDb!.CurrentPrice);
    }

    [Fact]
    public async Task DsePriceService_GetMarketSummary_UpdatesBenchmarkIndicesCorrectly()
    {
        using var context = CreateInMemoryDbContext();
        var (client, _) = CreateTestClient(req =>
        {
            var json = """
            {
                "ok": true,
                "indices": {
                    "DSEX": { "close": 5345.50, "change": 12.30, "change_percent": 0.23, "date": "2026-10-11" },
                    "DS30": { "close": 1940.20, "change": -4.10, "change_percent": -0.21, "date": "2026-10-11" },
                    "DSES": { "close": 1180.10, "change": 1.50, "change_percent": 0.13, "date": "2026-10-11" },
                    "CDSET": { "close": 1250.00, "change": 5.00, "change_percent": 0.40, "date": "2026-10-11" }
                },
                "breadth": {
                    "date": "2026-10-11",
                    "advanced": 180,
                    "declined": 140,
                    "unchanged": 75,
                    "total_volume": 120000000
                }
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var priceService = new DsePriceService(
            context,
            new HttpClient(new MockHttpMessageHandler(r => new HttpResponseMessage(HttpStatusCode.BadRequest))),
            client,
            null,
            NullLogger<DsePriceService>.Instance);

        var summary = await priceService.GetMarketSummaryAsync();

        Assert.NotNull(summary);
        Assert.Equal(4, summary.Indices.Count);

        // Verify benchmark points were recorded in database
        var dsexPoint = await context.BenchmarkPriceHistories.FirstOrDefaultAsync(b => b.BenchmarkCode == "DSEX");
        var cdsetPoint = await context.BenchmarkPriceHistories.FirstOrDefaultAsync(b => b.BenchmarkCode == "CDSET");

        Assert.NotNull(dsexPoint);
        Assert.Equal(5345.50m, dsexPoint.CloseValue);

        // Verify CDSET is strictly mapped to CDSET, not conflated with DSES
        Assert.NotNull(cdsetPoint);
        Assert.Equal(1250.00m, cdsetPoint.CloseValue);
    }
}
