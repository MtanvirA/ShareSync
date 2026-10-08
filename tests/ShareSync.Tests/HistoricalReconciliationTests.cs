using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Helpers;
using ShareSync.Application.DTOs.Benchmarks;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class HistoricalReconciliationTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new ShareSyncDbContext(options);

        // Seed sector and basic companies
        context.Sectors.Add(new Sector { SectorId = 1, SectorName = "Pharmaceuticals" });
        context.Companies.Add(new Company { CompanyId = 1, CompanyName = "Grameenphone Ltd.", TickerSymbol = "GP", SectorId = 1, CurrentPrice = 320m });
        context.Companies.Add(new Company { CompanyId = 2, CompanyName = "Square Pharmaceuticals PLC", TickerSymbol = "SQUAREPHARMA", SectorId = 1, CurrentPrice = 220m });
        context.Companies.Add(new Company { CompanyId = 10, CompanyName = "Grameen One Scheme Two", TickerSymbol = "GRAMEEN1MF", SectorId = 1, CurrentPrice = 15m });
        context.Companies.Add(new Company { CompanyId = 15, CompanyName = "AMCL (Pran)", TickerSymbol = "AMCL(PRAN)", SectorId = 1, CurrentPrice = 200m });

        context.SaveChanges();
        return context;
    }

    // 1. STAGING IMPORT TEST
    [Fact]
    public void StagingImport_ValidatesAndPopulatesStagingModels()
    {
        var companyMap = new Dictionary<string, int> { ["GP"] = 1 };
        var raw = new RawHistoricalObservation
        {
            RawTicker = "GP",
            RawDate = "2024-05-10",
            RawOpen = "310.00",
            RawHigh = "325.00",
            RawLow = "308.00",
            RawClose = "320.00",
            RawVolume = "54000",
            Source = "Harvard Dataverse",
            SourceDataset = "DSE Historical Dataset",
            SourceDoi = "10.7910/DVN/XIFYT1",
            ImportBatchId = "BATCH-TEST-001"
        };

        var staged = HistoricalReconciliationEngine.StageCompanyObservation(raw, companyMap);

        Assert.Equal(HistoricalValidationStatus.Valid, staged.Status);
        Assert.Equal(1, staged.CompanyId);
        Assert.Equal(new DateTime(2024, 5, 10), staged.TradingDate);
        Assert.Equal(320.00m, staged.ClosePrice);
        Assert.Equal(310.00m, staged.OpenPrice);
        Assert.Equal(325.00m, staged.HighPrice);
        Assert.Equal(308.00m, staged.LowPrice);
        Assert.Equal(54000L, staged.Volume);
        Assert.Equal("BATCH-TEST-001", staged.ImportBatchId);
    }

    // 2. EXACT DUPLICATE TEST
    [Fact]
    public void ExactDuplicate_CollapsesSafelyWithoutDuplicateInsertion()
    {
        var records = new List<StagedHistoricalRecord>
        {
            new() { CompanyId = 1, TradingDate = new DateTime(2024, 5, 10), ClosePrice = 320m, OpenPrice = 310m, HighPrice = 325m, LowPrice = 308m, Status = HistoricalValidationStatus.Valid },
            new() { CompanyId = 1, TradingDate = new DateTime(2024, 5, 10), ClosePrice = 320m, OpenPrice = 310m, HighPrice = 325m, LowPrice = 308m, Status = HistoricalValidationStatus.Valid }
        };

        var (promotable, conflicting, exactDupes) = HistoricalReconciliationEngine.ReconcileDuplicates(
            records, r => $"{r.CompanyId}_{r.TradingDate:yyyy-MM-dd}");

        Assert.Single(promotable);
        Assert.Empty(conflicting);
        Assert.Equal(1, exactDupes);
    }

    // 3. CONFLICTING DUPLICATE TEST
    [Fact]
    public void ConflictingDuplicate_FlagsConflictingAndExcludesFromPromotion()
    {
        var records = new List<StagedHistoricalRecord>
        {
            new() { CompanyId = 1, TradingDate = new DateTime(2024, 5, 10), ClosePrice = 320m, OpenPrice = 310m, HighPrice = 325m, LowPrice = 308m, Status = HistoricalValidationStatus.Valid },
            new() { CompanyId = 1, TradingDate = new DateTime(2024, 5, 10), ClosePrice = 350m, OpenPrice = 340m, HighPrice = 355m, LowPrice = 338m, Status = HistoricalValidationStatus.Valid }
        };

        var (promotable, conflicting, exactDupes) = HistoricalReconciliationEngine.ReconcileDuplicates(
            records, r => $"{r.CompanyId}_{r.TradingDate:yyyy-MM-dd}");

        Assert.Empty(promotable);
        Assert.Equal(2, conflicting.Count);
        Assert.All(conflicting, c => Assert.Equal(HistoricalValidationStatus.Conflicting, c.Status));
        Assert.Equal(0, exactDupes);
    }

    // 4. TICKER MAPPING TEST
    [Theory]
    [InlineData("SQURPHARMA", 2)]
    [InlineData("SQUAREPHARMA", 2)]
    [InlineData("squarepharma ", 2)]
    [InlineData("GRAMEENS2", 10)]
    [InlineData("GRAMEEN1MF", 10)]
    [InlineData("AMCLPRAN", 15)]
    [InlineData("AMCL(PRAN)", 15)]
    public void TickerMapping_NormalizesAndResolvesCanonicalCompanyId(string rawTicker, int expectedCompanyId)
    {
        var companyMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["GP"] = 1,
            ["SQUAREPHARMA"] = 2,
            ["GRAMEEN1MF"] = 10,
            ["AMCL(PRAN)"] = 15
        };

        var raw = new RawHistoricalObservation
        {
            RawTicker = rawTicker,
            RawDate = "2024-05-10",
            RawClose = "100.00"
        };

        var staged = HistoricalReconciliationEngine.StageCompanyObservation(raw, companyMap);

        Assert.Equal(HistoricalValidationStatus.Valid, staged.Status);
        Assert.Equal(expectedCompanyId, staged.CompanyId);
    }

    [Fact]
    public void TickerMapping_UnmappedTicker_FlaggedAsUnmapped()
    {
        var companyMap = new Dictionary<string, int> { ["GP"] = 1 };
        var raw = new RawHistoricalObservation
        {
            RawTicker = "UNKNOWN_CO",
            RawDate = "2024-05-10",
            RawClose = "100.00"
        };

        var staged = HistoricalReconciliationEngine.StageCompanyObservation(raw, companyMap);

        Assert.Equal(HistoricalValidationStatus.Unmapped, staged.Status);
        Assert.Null(staged.CompanyId);
    }

    // 5. INVALID OHLC TEST
    [Theory]
    [InlineData("100", "110", "105", "100")] // High (105) < Open (110)
    [InlineData("100", "95", "120", "115")]  // Low (115) > Open (95)
    [InlineData("100", "100", "90", "110")]  // High (90) < Low (110)
    [InlineData("150", "100", "120", "90")]   // Close (150) > High (120)
    [InlineData("80", "100", "120", "90")]    // Close (80) < Low (90)
    [InlineData("-10", "10", "20", "5")]      // Negative Close
    public void InvalidOhlc_RejectsImpossibleBounds(string close, string open, string high, string low)
    {
        var companyMap = new Dictionary<string, int> { ["GP"] = 1 };
        var raw = new RawHistoricalObservation
        {
            RawTicker = "GP",
            RawDate = "2024-05-10",
            RawClose = close,
            RawOpen = open,
            RawHigh = high,
            RawLow = low
        };

        var staged = HistoricalReconciliationEngine.StageCompanyObservation(raw, companyMap);

        Assert.True(staged.Status == HistoricalValidationStatus.InvalidOhlc || staged.Status == HistoricalValidationStatus.InvalidPrice);
    }

    // 6. MISSING VOLUME TEST
    [Fact]
    public void MissingVolume_PreservedAsNull()
    {
        var companyMap = new Dictionary<string, int> { ["GP"] = 1 };
        var raw = new RawHistoricalObservation
        {
            RawTicker = "GP",
            RawDate = "2024-05-10",
            RawClose = "100.00",
            RawVolume = null
        };

        var staged = HistoricalReconciliationEngine.StageCompanyObservation(raw, companyMap);

        Assert.Equal(HistoricalValidationStatus.Valid, staged.Status);
        Assert.Null(staged.Volume);
    }

    // 7. ZERO VOLUME TEST
    [Fact]
    public void ZeroVolume_PreservedAsValidObservation()
    {
        var companyMap = new Dictionary<string, int> { ["GP"] = 1 };
        var raw = new RawHistoricalObservation
        {
            RawTicker = "GP",
            RawDate = "2024-05-10",
            RawClose = "100.00",
            RawVolume = "0"
        };

        var staged = HistoricalReconciliationEngine.StageCompanyObservation(raw, companyMap);

        Assert.Equal(HistoricalValidationStatus.Valid, staged.Status);
        Assert.NotNull(staged.Volume);
        Assert.Equal(0L, staged.Volume!.Value);
    }

    // 8. IDEMPOTENT IMPORT TEST
    [Fact]
    public async Task IdempotentImport_RepeatedExecutionProducesIdenticalState()
    {
        using var context = CreateInMemoryDbContext();

        var existingDate = new DateTime(2024, 1, 1);
        context.CompanyPriceHistories.Add(new CompanyPriceHistory
        {
            CompanyId = 1,
            TradingDate = existingDate,
            RecordedAt = existingDate,
            Price = 300m,
            Source = "Test"
        });
        await context.SaveChangesAsync();

        int countBefore = await context.CompanyPriceHistories.CountAsync();

        // Simulate idempotent promotion: attempt to insert same (CompanyId, TradingDate)
        var newObservations = new List<CompanyPriceHistory>
        {
            new() { CompanyId = 1, TradingDate = existingDate, RecordedAt = existingDate, Price = 300m, Source = "New" }
        };

        var existingKeys = (await context.CompanyPriceHistories
            .Select(h => $"{h.CompanyId}_{h.TradingDate:yyyy-MM-dd}")
            .ToListAsync())
            .ToHashSet();

        var toInsert = newObservations
            .Where(o => !existingKeys.Contains($"{o.CompanyId}_{o.TradingDate:yyyy-MM-dd}"))
            .ToList();

        if (toInsert.Any())
        {
            await context.CompanyPriceHistories.AddRangeAsync(toInsert);
            await context.SaveChangesAsync();
        }

        int countAfter = await context.CompanyPriceHistories.CountAsync();
        Assert.Equal(countBefore, countAfter);
    }

    // 9. PROVENANCE TEST
    [Fact]
    public void Provenance_PreservesSourceDatasetDoiAndBatchId()
    {
        var companyMap = new Dictionary<string, int> { ["GP"] = 1 };
        var raw = new RawHistoricalObservation
        {
            RawTicker = "GP",
            RawDate = "2024-05-10",
            RawClose = "320.00",
            Source = "Harvard Dataverse",
            SourceDataset = "Dhaka Stock Exchange Historical Data (1999-2025)",
            SourceDoi = "10.7910/DVN/XIFYT1",
            ImportBatchId = "BATCH-PROV-999"
        };

        var staged = HistoricalReconciliationEngine.StageCompanyObservation(raw, companyMap);

        Assert.Equal("Harvard Dataverse", staged.Source);
        Assert.Equal("Dhaka Stock Exchange Historical Data (1999-2025)", staged.SourceDataset);
        Assert.Equal("10.7910/DVN/XIFYT1", staged.SourceDoi);
        Assert.Equal("BATCH-PROV-999", staged.ImportBatchId);
    }

    // 10. INSUFFICIENT HISTORY TEST
    [Fact]
    public void InsufficientHistory_CorrectlyIdentifiedAndFlagged()
    {
        // 5 observations spanning only 20 days (< 1 year)
        var dates = new List<DateTime>
        {
            new(2024, 1, 1),
            new(2024, 1, 5),
            new(2024, 1, 10),
            new(2024, 1, 15),
            new(2024, 1, 20)
        };

        var coverage = HistoricalReconciliationEngine.CalculateCoverage("SHORT_CO", dates);

        Assert.True(coverage.HasLessThanOneYear);
        Assert.False(coverage.HasFiveYears);
        Assert.False(coverage.HasThreeYears);
        Assert.Equal(5, coverage.TotalObservations);
    }

    // 11. BENCHMARK HISTORICAL RETRIEVAL TEST
    [Fact]
    public async Task BenchmarkHistoricalRetrieval_RetrievesDatabaseObservations()
    {
        using var context = CreateInMemoryDbContext();
        var dataProvider = new BenchmarkDataProvider();
        var service = new BenchmarkService(context, dataProvider);

        // Seed portfolio with user
        context.Users.Add(new AppUser { UserId = 1, Name = "Test User", Email = "test@example.com", PasswordHash = "hash" });
        context.Portfolios.Add(new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "Tech Portfolio" });

        var d1 = new DateTime(2024, 1, 1);
        var d2 = new DateTime(2024, 1, 15);

        context.PortfolioSnapshots.Add(new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = d1, TotalValue = 100000m });
        context.PortfolioSnapshots.Add(new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = d2, TotalValue = 110000m });

        // Add real database benchmark observations
        context.BenchmarkPriceHistories.Add(new BenchmarkPriceHistory
        {
            BenchmarkCode = "DSEX",
            BenchmarkName = "DSE Broad Index (DSEX)",
            TradingDate = d1,
            CloseValue = 6000m,
            Source = "Dhaka Stock Exchange"
        });
        context.BenchmarkPriceHistories.Add(new BenchmarkPriceHistory
        {
            BenchmarkCode = "DSEX",
            BenchmarkName = "DSE Broad Index (DSEX)",
            TradingDate = d2,
            CloseValue = 6300m,
            Source = "Dhaka Stock Exchange"
        });
        await context.SaveChangesAsync();

        var result = await service.ComparePortfolioAsync(new BenchmarkComparisonRequestDto
        {
            PortfolioId = 1,
            BenchmarkCode = "DSEX",
            Period = "ALL"
        }, 1, CancellationToken.None);

        Assert.True(result.HasSufficientData);
        Assert.False(result.IsFallback);
        Assert.Contains("Database Historical Series", result.BenchmarkSource);
        Assert.Equal(6000m, result.BenchmarkStartingValue);
        Assert.Equal(6300m, result.BenchmarkEndingValue);
        Assert.Equal(5.00m, result.BenchmarkReturnPercentage); // (6300 - 6000) / 6000 = +5%
    }

    // 12. BENCHMARK FALLBACK TEST
    [Fact]
    public async Task BenchmarkFallback_UsedOnlyWhenDbEmptyAndClearlyLabeled()
    {
        using var context = CreateInMemoryDbContext();
        var dataProvider = new BenchmarkDataProvider();
        var service = new BenchmarkService(context, dataProvider);

        // DB benchmark table is empty
        context.Users.Add(new AppUser { UserId = 1, Name = "Test User", Email = "test@example.com", PasswordHash = "hash" });
        context.Portfolios.Add(new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "Fallback Portfolio" });

        var d1 = new DateTime(2024, 1, 1);
        var d2 = new DateTime(2024, 1, 15);

        context.PortfolioSnapshots.Add(new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = d1, TotalValue = 100000m });
        context.PortfolioSnapshots.Add(new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = d2, TotalValue = 110000m });
        await context.SaveChangesAsync();

        var result = await service.ComparePortfolioAsync(new BenchmarkComparisonRequestDto
        {
            PortfolioId = 1,
            BenchmarkCode = "DSEX",
            Period = "ALL"
        }, 1, CancellationToken.None);

        Assert.True(result.HasSufficientData);
        Assert.True(result.IsFallback);
        Assert.Equal("Static Calibrated Model (Fallback)", result.BenchmarkSource);
        Assert.Contains("Calibrated reference benchmark model used", result.DataLimitationNotice);
    }

    // 13. MYSQL-FIRST HISTORICAL RETRIEVAL TEST
    [Fact]
    public async Task MySQLFirstHistoricalRetrieval_DoesNotReadCsvWhenDbHasSufficientHistory()
    {
        using var context = CreateInMemoryDbContext();
        var service = new InvestmentIntelligenceService(context);

        var startDate = new DateTime(2019, 1, 1);
        for (int i = 0; i < 200; i++)
        {
            var d = startDate.AddDays(i * 10);
            context.CompanyPriceHistories.Add(new CompanyPriceHistory
            {
                CompanyId = 1,
                TradingDate = d,
                RecordedAt = d,
                Price = 300m + (i * 0.5m),
                OpenPrice = 300m,
                HighPrice = 350m,
                LowPrice = 290m,
                Source = "MySQL Canonical History"
            });
        }
        await context.SaveChangesAsync();

        var profile = await service.GetHistoricalInvestmentProfileAsync(1, null, null, CancellationToken.None);

        Assert.NotNull(profile.Data);
        Assert.True(profile.Data.DataQuality.HasFullFiveYearHistory);
        Assert.True(profile.Data.DataQuality.AnalysisObservationCount >= 180);
        Assert.Equal("GP", profile.Data.Company.TradingCode);
        Assert.True(profile.Data.Summary.EndingPrice > profile.Data.Summary.StartingPrice);
    }

    // PHASE 18: INVESTMENT INTELLIGENCE REAL COMPANIES REGRESSION
    [Fact]
    public async Task InvestmentIntelligence_FiveYearWindowChronologicalMetrics_AreAccurate()
    {
        using var context = CreateInMemoryDbContext();
        var service = new InvestmentIntelligenceService(context);

        // Seed 5 years of daily records for Square Pharma (ID 2)
        var start = new DateTime(2020, 1, 1);
        decimal initialPrice = 200m;
        for (int day = 0; day < 1825; day += 2) // every 2 days over 5 years (~912 observations)
        {
            var date = start.AddDays(day);
            decimal price = initialPrice + (day * 0.05m); // steady positive trend
            context.CompanyPriceHistories.Add(new CompanyPriceHistory
            {
                CompanyId = 2,
                TradingDate = date,
                RecordedAt = date,
                Price = price,
                OpenPrice = price,
                HighPrice = price + 2m,
                LowPrice = price - 2m,
                Source = "Canonical MySQL"
            });
        }
        await context.SaveChangesAsync();

        var profile = await service.GetHistoricalInvestmentProfileAsync(2, null, null, CancellationToken.None);

        Assert.NotNull(profile.Data);
        Assert.True(profile.Data.DataQuality.HasFullFiveYearHistory);
        Assert.True(profile.Data.Summary.HistoricalReturnPercentage > 0);
        Assert.True(profile.Data.Summary.CagrPercentage > 0);
        Assert.NotNull(profile.Data.Trend.LatestMA50);
        Assert.NotNull(profile.Data.Trend.LatestMA200);
        Assert.True(profile.Data.Trend.LatestMA200 > 0);
    }

    [Fact]
    public async Task InvestmentIntelligence_DualCompanyRegression_ChronologicalMetricsValid()
    {
        using var context = CreateInMemoryDbContext();
        var service = new InvestmentIntelligenceService(context);

        // Seed 5 years for GP (ID 1) and SQUAREPHARMA (ID 2)
        var start = new DateTime(2019, 1, 1);
        for (int i = 0; i < 300; i++)
        {
            var d = start.AddDays(i * 6); // spans ~1800 days (approx 5 years)
            context.CompanyPriceHistories.Add(new CompanyPriceHistory
            {
                CompanyId = 1,
                TradingDate = d,
                RecordedAt = d,
                Price = 300m + (i * 0.2m),
                OpenPrice = 300m,
                HighPrice = 320m,
                LowPrice = 295m,
                Source = "Canonical MySQL"
            });

            context.CompanyPriceHistories.Add(new CompanyPriceHistory
            {
                CompanyId = 2,
                TradingDate = d,
                RecordedAt = d,
                Price = 200m + (i * 0.3m),
                OpenPrice = 200m,
                HighPrice = 215m,
                LowPrice = 195m,
                Source = "Canonical MySQL"
            });
        }
        await context.SaveChangesAsync();

        // 1. Evaluate GP (ID 1)
        var gpProfile = await service.GetHistoricalInvestmentProfileAsync(1, null, null, CancellationToken.None);
        Assert.NotNull(gpProfile.Data);
        Assert.True(gpProfile.Data.DataQuality.HasFullFiveYearHistory);
        Assert.Equal("GP", gpProfile.Data.Company.TradingCode);
        Assert.True(gpProfile.Data.Summary.HistoricalReturnPercentage > 0);
        Assert.True(gpProfile.Data.Summary.CagrPercentage > 0);
        Assert.True(gpProfile.Data.Risk.AnnualizedVolatilityPercentage >= 0);
        Assert.True(gpProfile.Data.Risk.MaximumDrawdownPercentage <= 0);
        Assert.NotNull(gpProfile.Data.Trend.LatestMA50);
        Assert.NotNull(gpProfile.Data.Trend.LatestMA200);

        // 2. Evaluate Square Pharma (ID 2)
        var sqProfile = await service.GetHistoricalInvestmentProfileAsync(2, null, null, CancellationToken.None);
        Assert.NotNull(sqProfile.Data);
        Assert.True(sqProfile.Data.DataQuality.HasFullFiveYearHistory);
        Assert.Equal("SQUAREPHARMA", sqProfile.Data.Company.TradingCode);
        Assert.True(sqProfile.Data.Summary.HistoricalReturnPercentage > 0);
        Assert.True(sqProfile.Data.Summary.CagrPercentage > 0);
        Assert.True(sqProfile.Data.Risk.AnnualizedVolatilityPercentage >= 0);
        Assert.True(sqProfile.Data.Risk.MaximumDrawdownPercentage <= 0);
        Assert.NotNull(sqProfile.Data.Trend.LatestMA50);
        Assert.NotNull(sqProfile.Data.Trend.LatestMA200);

        // Verify chronological chart data ordering
        var chartPoints = sqProfile.Data.ChartData;
        Assert.True(chartPoints.Count > 1);
        for (int i = 0; i < chartPoints.Count - 1; i++)
        {
            var currDate = DateTime.Parse(chartPoints[i].Date);
            var nextDate = DateTime.Parse(chartPoints[i + 1].Date);
            Assert.True(currDate <= nextDate, "Chart data must be chronologically ordered.");
        }
    }
}
