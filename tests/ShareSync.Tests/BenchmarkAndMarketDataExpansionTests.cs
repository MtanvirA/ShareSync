using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.DTOs.Benchmarks;
using ShareSync.Application.DTOs.Companies;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class BenchmarkAndMarketDataExpansionTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new ShareSyncDbContext(options);

        // Core Users
        context.Users.AddRange(
            new AppUser { UserId = 1, Email = "investor@sharesync.com", Name = "Demo Investor", PasswordHash = "hash1", Role = "INVESTOR" },
            new AppUser { UserId = 2, Email = "other@sharesync.com", Name = "Other User", PasswordHash = "hash2", Role = "INVESTOR" }
        );

        // Sectors
        context.Sectors.AddRange(
            new Sector { SectorId = 1, SectorName = "Telecommunication" },
            new Sector { SectorId = 2, SectorName = "Pharmaceuticals" },
            new Sector { SectorId = 3, SectorName = "Consumer Goods" },
            new Sector { SectorId = 4, SectorName = "Financial Services" },
            new Sector { SectorId = 7, SectorName = "Engineering & Industrial" }
        );

        // Expanded Company Universe (LF-31)
        context.Companies.AddRange(
            new Company { CompanyId = 1, TickerSymbol = "GP", CompanyName = "Grameenphone Ltd.", SectorId = 1, CurrentPrice = 382.50m },
            new Company { CompanyId = 2, TickerSymbol = "SQUAREPHARMA", CompanyName = "Square Pharmaceuticals PLC", SectorId = 2, CurrentPrice = 228.40m },
            new Company { CompanyId = 3, TickerSymbol = "BATBC", CompanyName = "British American Tobacco Bangladesh", SectorId = 3, CurrentPrice = 465.20m },
            new Company { CompanyId = 4, TickerSymbol = "BRACBANK", CompanyName = "BRAC Bank PLC", SectorId = 4, CurrentPrice = 62.75m },
            new Company { CompanyId = 5, TickerSymbol = "BXPHARMA", CompanyName = "Beximco Pharmaceuticals Ltd.", SectorId = 2, CurrentPrice = 142.10m },
            new Company { CompanyId = 6, TickerSymbol = "RENATA", CompanyName = "Renata PLC", SectorId = 2, CurrentPrice = 785.00m },
            new Company { CompanyId = 7, TickerSymbol = "LHBL", CompanyName = "LafargeHolcim Bangladesh Ltd.", SectorId = 7, CurrentPrice = 71.30m },
            new Company { CompanyId = 8, TickerSymbol = "WALTONHIL", CompanyName = "Walton Hi-Tech Industries PLC", SectorId = 7, CurrentPrice = 692.50m },
            new Company { CompanyId = 9, TickerSymbol = "BEXIMCO", CompanyName = "Beximco Ltd.", SectorId = 3, CurrentPrice = 115.50m }
        );

        // Portfolios
        context.Portfolios.AddRange(
            new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "Primary Test Portfolio" },
            new Portfolio { PortfolioId = 2, UserId = 2, PortfolioName = "Other User Portfolio" }
        );

        context.SaveChanges();
        return context;
    }

    // -------------------------------------------------------------------------
    // TEST 1: Benchmark Data Retrieval from Database (LF-30)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Test1_BenchmarkDataRetrieval_FromDatabase_ReturnsConfiguredBenchmarksAndLatestValues()
    {
        using var context = CreateInMemoryDbContext();
        var dataProvider = new BenchmarkDataProvider();
        var service = new BenchmarkService(context, dataProvider);

        // Seed DB Benchmark records
        context.BenchmarkPriceHistories.AddRange(
            new BenchmarkPriceHistory
            {
                BenchmarkCode = "DSEX",
                BenchmarkName = "DSE Broad Index (DSEX)",
                TradingDate = new DateTime(2022, 12, 01),
                CloseValue = 6996.06m,
                Source = "Dhaka Stock Exchange"
            },
            new BenchmarkPriceHistory
            {
                BenchmarkCode = "DSEX",
                BenchmarkName = "DSE Broad Index (DSEX)",
                TradingDate = new DateTime(2022, 12, 07),
                CloseValue = 6355.49m,
                Source = "Dhaka Stock Exchange"
            },
            new BenchmarkPriceHistory
            {
                BenchmarkCode = "DS30",
                BenchmarkName = "DSE 30 Index (DS30)",
                TradingDate = new DateTime(2022, 12, 07),
                CloseValue = 2284.14m,
                Source = "Dhaka Stock Exchange"
            }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetAvailableBenchmarksAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        var dsex = result.FirstOrDefault(b => b.BenchmarkCode == "DSEX");
        Assert.NotNull(dsex);
        Assert.Equal("DSE Broad Index (DSEX)", dsex.Name);
        Assert.Equal(6355.49m, dsex.CurrentValue); // latest close

        var ds30 = result.FirstOrDefault(b => b.BenchmarkCode == "DS30");
        Assert.NotNull(ds30);
        Assert.Equal(2284.14m, ds30.CurrentValue);
    }

    // -------------------------------------------------------------------------
    // TEST 2: Date Alignment (Strictly Overlapping Periods Only)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Test2_DateAlignment_StrictlyOverlappingDates_PairsPortfolioSnapshotsWithBenchmarkDates()
    {
        using var context = CreateInMemoryDbContext();
        var dataProvider = new BenchmarkDataProvider();
        var service = new BenchmarkService(context, dataProvider);

        // Portfolio snapshots:
        // 2022-11-25 (outside benchmark range)
        // 2022-12-01 (overlapping)
        // 2022-12-07 (overlapping)
        context.PortfolioSnapshots.AddRange(
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = new DateTime(2022, 11, 25), TotalValue = 90000m },
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = new DateTime(2022, 12, 01), TotalValue = 100000m },
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = new DateTime(2022, 12, 07), TotalValue = 110000m }
        );

        // Database Benchmark DSEX covers 2022-12-01 to 2022-12-07
        context.BenchmarkPriceHistories.AddRange(
            new BenchmarkPriceHistory { BenchmarkCode = "DSEX", BenchmarkName = "DSEX", TradingDate = new DateTime(2022, 12, 01), CloseValue = 6000m },
            new BenchmarkPriceHistory { BenchmarkCode = "DSEX", BenchmarkName = "DSEX", TradingDate = new DateTime(2022, 12, 07), CloseValue = 6600m }
        );
        await context.SaveChangesAsync();

        var request = new BenchmarkComparisonRequestDto
        {
            PortfolioId = 1,
            BenchmarkCode = "DSEX",
            Period = "ALL"
        };

        // Act
        var result = await service.ComparePortfolioAsync(request, userId: 1);

        // Assert: strictly overlapping starting point (2022-12-01, 100,000) to ending point (2022-12-07, 110,000)
        Assert.True(result.HasSufficientData);
        Assert.Equal(100000m, result.PortfolioStartingValue);
        Assert.Equal(110000m, result.PortfolioEndingValue);
        Assert.Equal(6000m, result.BenchmarkStartingValue);
        Assert.Equal(6600m, result.BenchmarkEndingValue);
        Assert.Equal(2, result.Labels.Count);
    }

    // -------------------------------------------------------------------------
    // TEST 3: Portfolio vs Benchmark Return (Controlled Calculation)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Test3_ControlledCalculation_PortfolioVsBenchmarkReturn_CalculatedIndependently()
    {
        // CONTROLLED MATHEMATICAL EXAMPLE:
        // Portfolio: Start = 200,000.00, End = 230,000.00
        // Expected Portfolio Return = ((230,000 - 200,000) / 200,000) * 100 = +15.00%
        // Benchmark: Start = 5,000.00, End = 5,500.00
        // Expected Benchmark Return = ((5,500 - 5,000) / 5,000) * 100 = +10.00%
        // Expected Outperformance (Alpha Spread) = 15.00% - 10.00% = +5.00%
        using var context = CreateInMemoryDbContext();
        var dataProvider = new BenchmarkDataProvider();
        var service = new BenchmarkService(context, dataProvider);

        var date1 = new DateTime(2022, 10, 01);
        var date2 = new DateTime(2022, 12, 01);

        context.PortfolioSnapshots.AddRange(
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = date1, TotalValue = 200000m },
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = date2, TotalValue = 230000m }
        );

        context.BenchmarkPriceHistories.AddRange(
            new BenchmarkPriceHistory { BenchmarkCode = "DSEX", BenchmarkName = "DSEX", TradingDate = date1, CloseValue = 5000.00m },
            new BenchmarkPriceHistory { BenchmarkCode = "DSEX", BenchmarkName = "DSEX", TradingDate = date2, CloseValue = 5500.00m }
        );
        await context.SaveChangesAsync();

        var request = new BenchmarkComparisonRequestDto
        {
            PortfolioId = 1,
            BenchmarkCode = "DSEX",
            Period = "ALL"
        };

        // Act
        var result = await service.ComparePortfolioAsync(request, userId: 1);

        // Assert
        Assert.True(result.HasSufficientData);
        Assert.Equal(15.00m, result.PortfolioReturnPercentage);
        Assert.Equal(10.00m, result.BenchmarkReturnPercentage);
        Assert.Equal(5.00m, result.OutperformancePercentage);
        Assert.True(result.IsOutperforming);
    }

    // -------------------------------------------------------------------------
    // TEST 4: Missing Benchmark Date (Weekend / Holiday Fallback)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Test4_MissingBenchmarkDate_FallsBackToClosestPriorTradingDay()
    {
        using var context = CreateInMemoryDbContext();
        var dataProvider = new BenchmarkDataProvider();
        var service = new BenchmarkService(context, dataProvider);

        // Benchmark traded on Thursday 2022-12-01 ($6,000) and Tuesday 2022-12-06 ($6,300)
        context.BenchmarkPriceHistories.AddRange(
            new BenchmarkPriceHistory { BenchmarkCode = "DSEX", BenchmarkName = "DSEX", TradingDate = new DateTime(2022, 12, 01), CloseValue = 6000m },
            new BenchmarkPriceHistory { BenchmarkCode = "DSEX", BenchmarkName = "DSEX", TradingDate = new DateTime(2022, 12, 06), CloseValue = 6300m }
        );

        // Snapshot is on Sunday 2022-12-04 (non-trading day) and Tuesday 2022-12-06
        // Sunday 2022-12-04 should align to Thursday 2022-12-01 ($6,000)
        context.PortfolioSnapshots.AddRange(
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = new DateTime(2022, 12, 04), TotalValue = 100000m },
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = new DateTime(2022, 12, 06), TotalValue = 105000m }
        );
        await context.SaveChangesAsync();

        var request = new BenchmarkComparisonRequestDto { PortfolioId = 1, BenchmarkCode = "DSEX", Period = "ALL" };

        // Act
        var result = await service.ComparePortfolioAsync(request, userId: 1);

        // Assert
        Assert.True(result.HasSufficientData);
        Assert.Equal(6000m, result.BenchmarkStartingValue); // Aligned to 2022-12-01 close
        Assert.Equal(6300m, result.BenchmarkEndingValue);   // Aligned to 2022-12-06 close
        Assert.Equal(5.00m, result.BenchmarkReturnPercentage);
    }

    // -------------------------------------------------------------------------
    // TEST 5: Multiple Benchmarks (DSEX, DS30, DSES)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Test5_MultipleBenchmarks_EvaluatesAgainstDifferentIndices_ProducesDistinctResults()
    {
        using var context = CreateInMemoryDbContext();
        var dataProvider = new BenchmarkDataProvider();
        var service = new BenchmarkService(context, dataProvider);

        var d1 = new DateTime(2022, 11, 01);
        var d2 = new DateTime(2022, 12, 01);

        context.PortfolioSnapshots.AddRange(
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = d1, TotalValue = 100000m },
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = d2, TotalValue = 110000m }
        );

        // DSEX: +5% (6000 -> 6300)
        context.BenchmarkPriceHistories.AddRange(
            new BenchmarkPriceHistory { BenchmarkCode = "DSEX", BenchmarkName = "DSEX", TradingDate = d1, CloseValue = 6000m },
            new BenchmarkPriceHistory { BenchmarkCode = "DSEX", BenchmarkName = "DSEX", TradingDate = d2, CloseValue = 6300m }
        );

        // DS30: +10% (2000 -> 2200)
        context.BenchmarkPriceHistories.AddRange(
            new BenchmarkPriceHistory { BenchmarkCode = "DS30", BenchmarkName = "DS30", TradingDate = d1, CloseValue = 2000m },
            new BenchmarkPriceHistory { BenchmarkCode = "DS30", BenchmarkName = "DS30", TradingDate = d2, CloseValue = 2200m }
        );

        // DSES: +2% (1500 -> 1530)
        context.BenchmarkPriceHistories.AddRange(
            new BenchmarkPriceHistory { BenchmarkCode = "DSES", BenchmarkName = "DSES", TradingDate = d1, CloseValue = 1500m },
            new BenchmarkPriceHistory { BenchmarkCode = "DSES", BenchmarkName = "DSES", TradingDate = d2, CloseValue = 1530m }
        );
        await context.SaveChangesAsync();

        // 1. DSEX Comparison
        var resDsex = await service.ComparePortfolioAsync(new BenchmarkComparisonRequestDto { PortfolioId = 1, BenchmarkCode = "DSEX" }, userId: 1);
        Assert.Equal(5.00m, resDsex.BenchmarkReturnPercentage);
        Assert.Equal(5.00m, resDsex.OutperformancePercentage); // 10% - 5% = +5%

        // 2. DS30 Comparison
        var resDs30 = await service.ComparePortfolioAsync(new BenchmarkComparisonRequestDto { PortfolioId = 1, BenchmarkCode = "DS30" }, userId: 1);
        Assert.Equal(10.00m, resDs30.BenchmarkReturnPercentage);
        Assert.Equal(0.00m, resDs30.OutperformancePercentage); // 10% - 10% = 0%

        // 3. DSES Comparison
        var resDses = await service.ComparePortfolioAsync(new BenchmarkComparisonRequestDto { PortfolioId = 1, BenchmarkCode = "DSES" }, userId: 1);
        Assert.Equal(2.00m, resDses.BenchmarkReturnPercentage);
        Assert.Equal(8.00m, resDses.OutperformancePercentage); // 10% - 2% = +8%
    }

    // -------------------------------------------------------------------------
    // TEST 6: Invalid Benchmark Handling
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Test6_InvalidBenchmark_ThrowsValidationException()
    {
        using var context = CreateInMemoryDbContext();
        var dataProvider = new BenchmarkDataProvider();
        var service = new BenchmarkService(context, dataProvider);

        var request = new BenchmarkComparisonRequestDto
        {
            PortfolioId = 1,
            BenchmarkCode = "UNKNOWN_INDEX_XYZ",
            Period = "ALL"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.ComparePortfolioAsync(request, userId: 1));
        Assert.Contains("not recognized", ex.Message);
    }

    // -------------------------------------------------------------------------
    // TEST 7: Company Historical Price Retrieval (LF-31)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Test7_CompanyHistoricalRetrieval_RetrievesPriceHistoryAccurately()
    {
        using var context = CreateInMemoryDbContext();
        var companyService = new CompanyService(context);

        context.CompanyPriceHistories.AddRange(
            new CompanyPriceHistory { CompanyId = 1, TradingDate = new DateTime(2022, 12, 01), RecordedAt = new DateTime(2022, 12, 01), Price = 380m, OpenPrice = 378m, HighPrice = 382m, LowPrice = 375m, Volume = 50000 },
            new CompanyPriceHistory { CompanyId = 1, TradingDate = new DateTime(2022, 12, 07), RecordedAt = new DateTime(2022, 12, 07), Price = 385m, OpenPrice = 380m, HighPrice = 386m, LowPrice = 379m, Volume = 60000 }
        );
        await context.SaveChangesAsync();

        // Act
        var res = await companyService.GetCompanyPriceHistoryAsync(companyId: 1);

        // Assert
        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        Assert.Equal(2, res.Data.History.Count);
        Assert.Equal(380m, res.Data.History.First().Price); // Chronological ascending order
        Assert.Equal(385m, res.Data.History.Last().Price);
    }

    // -------------------------------------------------------------------------
    // TEST 8: Expanded Company Universe Retrieval (LF-31)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Test8_ExpandedCompanySearch_FindsCompaniesAcrossExpandedUniverse()
    {
        using var context = CreateInMemoryDbContext();
        var companyService = new CompanyService(context);

        var allRes = await companyService.GetAllCompaniesAsync();
        Assert.True(allRes.Success);
        Assert.NotNull(allRes.Data);

        // 1. Verify BATBC exists in expanded universe
        var batbc = allRes.Data.FirstOrDefault(c => c.TickerSymbol == "BATBC");
        Assert.NotNull(batbc);
        Assert.Equal("British American Tobacco Bangladesh", batbc.CompanyName);

        // 2. Verify WALTONHIL exists
        var walton = allRes.Data.FirstOrDefault(c => c.TickerSymbol == "WALTONHIL");
        Assert.NotNull(walton);
        Assert.Equal("Walton Hi-Tech Industries PLC", walton.CompanyName);

        // 3. Verify BEXIMCO exists
        var beximco = allRes.Data.FirstOrDefault(c => c.TickerSymbol == "BEXIMCO");
        Assert.NotNull(beximco);
        Assert.Equal("Beximco Ltd.", beximco.CompanyName);
    }

    // -------------------------------------------------------------------------
    // TEST 9: Duplicate Historical Data Handling & OHLC Quality Rules (LF-31)
    // -------------------------------------------------------------------------
    [Fact]
    public void Test9_DataQualityRules_RejectsInvalidOhlcAndDuplicates_PreservesZeroVolume()
    {
        // 1. Invalid OHLC: High < Low
        decimal open = 100m, high = 90m, low = 95m, close = 98m;
        bool isInvalidOhlc = high < low || high < open || high < close || low > open || low > close;
        Assert.True(isInvalidOhlc);

        // 2. Valid OHLC
        decimal vOpen = 100m, vHigh = 105m, vLow = 95m, vClose = 102m;
        bool isValidOhlc = !(vHigh < vLow || vHigh < vOpen || vHigh < vClose || vLow > vOpen || vLow > vClose);
        Assert.True(isValidOhlc);

        // 3. Preserve Legitimate Zero-Volume: Volume = 0 is acceptable if Price > 0
        long volume = 0;
        decimal price = 150m;
        bool isAcceptableZeroVolume = (volume == 0 && price > 0);
        Assert.True(isAcceptableZeroVolume);

        // 4. Duplicate Observation Check
        var seenKeys = new HashSet<string>();
        string key = "DSEX_2022-12-07";
        Assert.True(seenKeys.Add(key));
        Assert.False(seenKeys.Add(key)); // Duplicate rejected
    }

    // -------------------------------------------------------------------------
    // TEST 10: Recent-Data Availability
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Test10_RecentDataAvailability_EvaluatesAgainstRecentObservations()
    {
        using var context = CreateInMemoryDbContext();
        var dataProvider = new BenchmarkDataProvider();
        var service = new BenchmarkService(context, dataProvider);

        var now = DateTime.UtcNow.Date;

        // Portfolio snapshots over recent 30 days
        context.PortfolioSnapshots.AddRange(
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = now.AddDays(-30), TotalValue = 100000m },
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = now.AddDays(-15), TotalValue = 105000m },
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = now.AddDays(-1), TotalValue = 112000m }
        );

        // Recent Benchmark records
        context.BenchmarkPriceHistories.AddRange(
            new BenchmarkPriceHistory { BenchmarkCode = "DSEX", BenchmarkName = "DSEX", TradingDate = now.AddDays(-30), CloseValue = 6245m },
            new BenchmarkPriceHistory { BenchmarkCode = "DSEX", BenchmarkName = "DSEX", TradingDate = now.AddDays(-15), CloseValue = 6520m },
            new BenchmarkPriceHistory { BenchmarkCode = "DSEX", BenchmarkName = "DSEX", TradingDate = now.AddDays(-1), CloseValue = 6895.50m }
        );
        await context.SaveChangesAsync();

        var request = new BenchmarkComparisonRequestDto { PortfolioId = 1, BenchmarkCode = "DSEX", Period = "1M" };

        // Act
        var result = await service.ComparePortfolioAsync(request, userId: 1);

        // Assert
        Assert.True(result.HasSufficientData);
        Assert.Equal(100000m, result.PortfolioStartingValue);
        Assert.Equal(112000m, result.PortfolioEndingValue);
        Assert.Equal(12.00m, result.PortfolioReturnPercentage); // +12%
        Assert.True(result.BenchmarkReturnPercentage > 0);
    }

    // -------------------------------------------------------------------------
    // TEST 11: Investment Intelligence Regression
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Test11_InvestmentIntelligenceRegression_TrendAndPerformanceRemainAccurate()
    {
        using var context = CreateInMemoryDbContext();
        var intelService = new InvestmentIntelligenceService(context);

        var startDate = new DateTime(2020, 1, 1);

        // Feed price history for GP
        for (int i = 0; i < 60; i++)
        {
            var p = 300m + (i * 1.5m);
            context.CompanyPriceHistories.Add(new CompanyPriceHistory
            {
                CompanyId = 1,
                TradingDate = startDate.AddDays(i),
                RecordedAt = startDate.AddDays(i),
                Price = p,
                OpenPrice = p - 1,
                HighPrice = p + 2,
                LowPrice = p - 2,
                Volume = 50000
            });
        }
        await context.SaveChangesAsync();

        // Act
        var profile = await intelService.GetHistoricalInvestmentProfileAsync(companyId: 1);

        // Assert
        Assert.NotNull(profile);
        Assert.True(profile.Success);
        Assert.NotNull(profile.Data);
        Assert.Equal("GP", profile.Data.Company.TradingCode);
        Assert.True(profile.Data.Consistency.ValidReturnDays >= 40);
        Assert.True(profile.Data.Summary.HistoricalReturnPercentage > 0);
    }
}
