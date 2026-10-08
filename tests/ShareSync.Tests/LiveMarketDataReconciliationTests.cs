using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using ShareSync.Application.Common.Helpers;
using ShareSync.Application.DTOs.Benchmarks;
using ShareSync.Application.DTOs.InvestmentIntelligence;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using ShareSync.Infrastructure.Services;
using Xunit;

namespace ShareSync.Tests;

public class LiveMarketDataReconciliationTests
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
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ShareSyncDbContext(options);
    }

    // =========================================================================
    // 1. VALID LIVE PRICE & OHLC PARSING
    // =========================================================================
    [Fact]
    public async Task FetchQuoteDetailsAsync_ValidLivePrice_ParsesAccuratelyWithLiveStatus()
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
                    open = 220.00m,
                    high = 225.00m,
                    low = 219.50m,
                    close = 224.00m,
                    volume = 350000L,
                    timestamp = DateTime.UtcNow.AddMinutes(-5).ToString("yyyy-MM-dd HH:mm:ss")
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var service = new DsePriceService(context, new System.Net.Http.HttpClient(handler), NullLogger<DsePriceService>.Instance);
        var quote = await service.FetchQuoteDetailsAsync("SQUAREPHARMA");

        Assert.NotNull(quote);
        Assert.Equal(224.00m, quote.Price);
        Assert.Equal(220.00m, quote.OpenPrice);
        Assert.Equal(225.00m, quote.HighPrice);
        Assert.Equal(219.50m, quote.LowPrice);
        Assert.Equal(350000L, quote.Volume);
        Assert.Equal("LIVE", quote.Status);
        Assert.Equal("VALID", quote.ValidationState);
        Assert.Equal("StockChartBD Live API", quote.Source);
    }

    // =========================================================================
    // 2. MALFORMED RESPONSE
    // =========================================================================
    [Fact]
    public async Task FetchQuoteDetailsAsync_MalformedResponse_SafelyReturnsNull()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent("<html><head><title>Error</title></head><body>502 Bad Gateway</body></html>")
            };
        });

        var service = new DsePriceService(context, new System.Net.Http.HttpClient(handler), NullLogger<DsePriceService>.Instance);
        var quote = await service.FetchQuoteDetailsAsync("GP");

        Assert.Null(quote);
    }

    // =========================================================================
    // 3. MISSING PRICE IN QUOTE
    // =========================================================================
    [Fact]
    public async Task FetchQuoteDetailsAsync_MissingPrice_SafelyReturnsNull()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                quote = new
                {
                    symbol = "BATBC",
                    volume = 12000L
                    // close and open intentionally omitted
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var service = new DsePriceService(context, new System.Net.Http.HttpClient(handler), NullLogger<DsePriceService>.Instance);
        var quote = await service.FetchQuoteDetailsAsync("BATBC");

        Assert.Null(quote);
    }

    // =========================================================================
    // 4. INVALID NON-POSITIVE OR OUT-OF-BOUNDS PRICE
    // =========================================================================
    [Theory]
    [InlineData(0.00)]
    [InlineData(-25.50)]
    [InlineData(600000.00)]
    public async Task FetchQuoteDetailsAsync_InvalidPrice_RejectedBySanityValidator(decimal invalidPrice)
    {
        using var context = CreateInMemoryDbContext();
        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                quote = new
                {
                    symbol = "RENATA",
                    close = invalidPrice,
                    open = invalidPrice
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var service = new DsePriceService(context, new System.Net.Http.HttpClient(handler), NullLogger<DsePriceService>.Instance);
        var quote = await service.FetchQuoteDetailsAsync("RENATA");

        Assert.Null(quote);
    }

    // =========================================================================
    // 5. STALE DATA DETECTION
    // =========================================================================
    [Fact]
    public async Task FetchQuoteDetailsAsync_StaleTimestamp_FlaggedAsStale()
    {
        using var context = CreateInMemoryDbContext();
        var staleDate = DateTime.UtcNow.AddDays(-14);
        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                quote = new
                {
                    symbol = "MARICO",
                    close = 2450.00m,
                    open = 2440.00m,
                    high = 2460.00m,
                    low = 2435.00m,
                    timestamp = staleDate.ToString("yyyy-MM-dd HH:mm:ss")
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var service = new DsePriceService(context, new System.Net.Http.HttpClient(handler), NullLogger<DsePriceService>.Instance);
        var quote = await service.FetchQuoteDetailsAsync("MARICO");

        Assert.NotNull(quote);
        Assert.Equal(2450.00m, quote.Price);
        Assert.Equal("STALE", quote.Status);
    }

    // =========================================================================
    // 6. FUTURE TIMESTAMP REJECTION
    // =========================================================================
    [Fact]
    public async Task FetchQuoteDetailsAsync_FutureTimestamp_RejectedAsAnomaly()
    {
        using var context = CreateInMemoryDbContext();
        var futureDate = DateTime.UtcNow.AddDays(2);
        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                quote = new
                {
                    symbol = "LHBL",
                    close = 72.50m,
                    timestamp = futureDate.ToString("yyyy-MM-dd HH:mm:ss")
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var service = new DsePriceService(context, new System.Net.Http.HttpClient(handler), NullLogger<DsePriceService>.Instance);
        var quote = await service.FetchQuoteDetailsAsync("LHBL");

        Assert.Null(quote); // Future timestamp strictly rejected
    }

    // =========================================================================
    // 7. SOURCE CONFLICT / EXTREME DEVIATION REJECTION
    // =========================================================================
    [Fact]
    public async Task SyncAllCompanyPricesAsync_ExtremeDeviation_RejectedAndFlaggedAsAnomaly()
    {
        using var context = CreateInMemoryDbContext();
        context.Companies.Add(new Company { CompanyId = 1, TickerSymbol = "GP", CompanyName = "Grameenphone", CurrentPrice = 300.00m });
        await context.SaveChangesAsync();

        // Feed proposes 15x jump (4,500.00m)
        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                quote = new { symbol = "GP", close = 4500.00m }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var service = new DsePriceService(context, new System.Net.Http.HttpClient(handler), NullLogger<DsePriceService>.Instance);
        var result = await service.SyncAllCompanyPricesAsync();

        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.UpdatedCount);
        Assert.Equal(300.00m, result.Data.UpdatedPrices["GP"]); // Preserved existing price
        Assert.Equal("REJECTED_ANOMALY", result.Data.PriceProvenance["GP"]);

        var gpInDb = await context.Companies.FindAsync(1);
        Assert.Equal(300.00m, gpInDb!.CurrentPrice);
    }

    // =========================================================================
    // 8. SOURCE UNAVAILABLE (HTTP 503 / NETWORK ERROR)
    // =========================================================================
    [Fact]
    public async Task SyncAllCompanyPricesAsync_SourceUnavailable_GracefullyRetainsExistingPrice()
    {
        using var context = CreateInMemoryDbContext();
        context.Companies.Add(new Company { CompanyId = 1, TickerSymbol = "BRACBANK", CompanyName = "BRAC Bank", CurrentPrice = 45.00m });
        await context.SaveChangesAsync();

        var handler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });

        var service = new DsePriceService(context, new System.Net.Http.HttpClient(handler), NullLogger<DsePriceService>.Instance);
        var result = await service.SyncAllCompanyPricesAsync();

        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.UpdatedCount);
        Assert.Equal(45.00m, result.Data.UpdatedPrices["BRACBANK"]);
        Assert.Equal("PRESERVED_FALLBACK", result.Data.PriceProvenance["BRACBANK"]);
    }

    // =========================================================================
    // 9. IDEMPOTENT SYNCHRONIZATION
    // =========================================================================
    [Fact]
    public async Task SyncAllCompanyPricesAsync_MultipleRuns_UpdatesSnapshotWithoutDuplicateRows()
    {
        using var context = CreateInMemoryDbContext();
        context.Companies.Add(new Company { CompanyId = 1, TickerSymbol = "GP", CompanyName = "Grameenphone", CurrentPrice = 280.00m });
        await context.SaveChangesAsync();

        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                quote = new { symbol = "GP", close = 285.00m, open = 282.00m, high = 287.00m, low = 281.00m, volume = 50000L }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var service = new DsePriceService(context, new System.Net.Http.HttpClient(handler), NullLogger<DsePriceService>.Instance);

        // Run 1
        var run1 = await service.SyncAllCompanyPricesAsync();
        Assert.Equal(1, run1.Data!.UpdatedCount);
        Assert.Equal(1, run1.Data.HistoryCount);

        // Run 2 (Same day, slightly different price)
        var run2 = await service.SyncAllCompanyPricesAsync();
        Assert.Equal(0, run2.Data!.HistoryCount); // Updated existing record, no new row!

        var totalHistoryRows = await context.CompanyPriceHistories.CountAsync(h => h.CompanyId == 1);
        Assert.Equal(1, totalHistoryRows); // Exactly 1 row for today
    }

    // =========================================================================
    // 10. TICKER CANONICALIZATION & NORMALIZATION
    // =========================================================================
    [Theory]
    [InlineData("  gp  ", "GP")]
    [InlineData("SQURPHARMA", "SQUAREPHARMA")]
    [InlineData("SQUARE", "SQUAREPHARMA")]
    [InlineData("BRAC", "BRACBANK")]
    [InlineData("WALTON", "WALTONHIL")]
    [InlineData("DSEX", "DSEX")]
    public void MarketDataPathResolver_NormalizeAndResolveCanonical_MapsCorrectly(string input, string expected)
    {
        var canonical = MarketDataPathResolver.ResolveCanonicalTicker(input);
        Assert.Equal(expected, canonical);
    }

    // =========================================================================
    // 11. PORTFOLIO PROTECTION AGAINST CORRUPT INCOMING PRICE
    // =========================================================================
    [Fact]
    public async Task PortfolioProtection_CorruptIncomingPrice_PreservesPortfolioValuation()
    {
        using var context = CreateInMemoryDbContext();
        context.Companies.Add(new Company { CompanyId = 1, TickerSymbol = "COMPA", CompanyName = "Company A", CurrentPrice = 100.00m });
        context.Portfolios.Add(new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "Test Portfolio" });
        context.Transactions.Add(new Transaction
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100m,
            PricePerShare = 100m,
            Fee = 0m,
            TransactionDate = DateTime.UtcNow.AddDays(-10)
        });
        await context.SaveChangesAsync();

        // Report service before corrupted sync
        var reportService = new ReportService(context);
        var beforeReport = await reportService.GetHoldingsReportAsync(1, 1);
        Assert.Equal(10000.00m, beforeReport.Data!.TotalMarketValue);
        Assert.Equal(0.00m, beforeReport.Data.TotalUnrealizedProfitLoss);

        // Attempt sync with corrupt 0.01x price (1.00m instead of 100.00m)
        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                quote = new { symbol = "COMPA", close = 1.00m }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var priceService = new DsePriceService(context, new System.Net.Http.HttpClient(handler), NullLogger<DsePriceService>.Instance);
        var syncResult = await priceService.SyncAllCompanyPricesAsync();

        Assert.Equal("REJECTED_ANOMALY", syncResult.Data!.PriceProvenance["COMPA"]);

        // Report service after corrupted sync
        var afterReport = await reportService.GetHoldingsReportAsync(1, 1);
        Assert.Equal(10000.00m, afterReport.Data!.TotalMarketValue); // Kept safe at 10,000 BDT
        Assert.Equal(0.00m, afterReport.Data.TotalUnrealizedProfitLoss);
    }

    // =========================================================================
    // 12. PROVENANCE LABELING IN HISTORY SNAPSHOT
    // =========================================================================
    [Fact]
    public async Task SyncAllCompanyPricesAsync_SuccessfulSync_StoresProvenanceMetadata()
    {
        using var context = CreateInMemoryDbContext();
        context.Companies.Add(new Company { CompanyId = 1, TickerSymbol = "GP", CompanyName = "Grameenphone", CurrentPrice = 250.00m });
        await context.SaveChangesAsync();

        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(new
            {
                ok = true,
                quote = new { symbol = "GP", close = 255.00m }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var service = new DsePriceService(context, new System.Net.Http.HttpClient(handler), NullLogger<DsePriceService>.Instance);
        await service.SyncAllCompanyPricesAsync();

        var history = await context.CompanyPriceHistories.FirstOrDefaultAsync(h => h.CompanyId == 1);
        Assert.NotNull(history);
        Assert.Equal("StockChartBD Live API", history.Source);
        Assert.Equal("DSE Real-Time Market Feed", history.SourceDataset);
        Assert.StartsWith("SYNC_", history.ImportBatchId);
    }

    // =========================================================================
    // 13. PHASE 18-A: BENCHMARK ATTRIBUTION TERMINOLOGY (ACTIVE RETURN)
    // =========================================================================
    [Fact]
    public void BenchmarkComparisonDto_TerminologyAudit_ProvidesActiveReturnAndAttributionLabels()
    {
        var dto = new BenchmarkComparisonDto
        {
            PortfolioReturnPercentage = 15.50m,
            BenchmarkReturnPercentage = 10.00m,
            OutperformancePercentage = 5.50m,
            IsOutperforming = true
        };

        Assert.Equal(5.50m, dto.ActiveReturnPercentage);
        Assert.Equal("Benchmark Outperformance", dto.AttributionMetricLabel);
    }

    // =========================================================================
    // 14. PHASE 18-B: SUB-ONE-YEAR PERIOD RETURN LABELING
    // =========================================================================
    [Fact]
    public async Task InvestmentIntelligence_SubOneYearPeriod_LabelsAsPeriodReturn()
    {
        using var context = CreateInMemoryDbContext();
        context.Sectors.Add(new Sector { SectorId = 1, SectorName = "Banking" });
        context.Companies.Add(new Company { CompanyId = 1, CompanyName = "Bank A", TickerSymbol = "BANKA", SectorId = 1, CurrentPrice = 50m });

        // Add 6 months of observations (less than 1 year)
        var start = new DateTime(2024, 1, 1);
        for (int i = 0; i < 60; i++)
        {
            var d = start.AddDays(i * 3);
            context.CompanyPriceHistories.Add(new CompanyPriceHistory
            {
                CompanyId = 1,
                TradingDate = d,
                RecordedAt = d,
                Price = 40m + (i * 0.2m),
                Source = "Canonical MySQL"
            });
        }
        await context.SaveChangesAsync();

        var service = new InvestmentIntelligenceService(context);
        var profile = await service.GetHistoricalInvestmentProfileAsync(1, start, start.AddDays(180));

        Assert.NotNull(profile.Data);
        Assert.Equal("Period Return", profile.Data.Summary.AnnualizedMetricLabel);
    }
}
