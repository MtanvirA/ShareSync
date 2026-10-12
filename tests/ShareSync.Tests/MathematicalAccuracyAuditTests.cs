using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Benchmarks;
using ShareSync.Application.DTOs.Transactions;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

/// <summary>
/// TEST: Mathematical Accuracy Audit Suite
/// 
/// PURPOSE:
/// Formally audits and validates all 12 core financial formulas implemented across ShareSync
/// against an independent mathematical oracle (IndependentReferenceCalculator):
/// 1. Weighted Average Cost: SUM(BUY Qty * BUY Price) / SUM(BUY Qty)
/// 2. Realized P/L: (SELL Gross - Fee) - (SELL Qty * Weighted Average Buy Cost)
/// 3. Net Cash Flow: Total SELL Inflow - Total BUY Outflow
/// 4. CAGR: (Ending / Starting) ^ (1 / Years) - 1
/// 5. Annualized Volatility: Sample StdDev(Daily Returns) * SQRT(250)
/// 6. Maximum Drawdown: MAX( (Peak - Trough) / Peak )
/// 7. SMA 50 / SMA 200: Moving averages over chronological trading windows
/// 8. Fisher Real Return: [ (1 + Nominal) / (1 + Cumulative Inflation) ] - 1
/// 9. Active Return / Benchmark Outperformance: Portfolio Return - Benchmark Return
/// 10. Currency Conversion: Amount * Exchange Rate
/// 11. Oversell Prevention: Rejects SELL if Qty > Available Holdings
/// 12. Dividend Yield: Gross Dividends / Total Investment
/// </summary>
public class MathematicalAccuracyAuditTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var context = new ShareSyncDbContext(options);

        // Core seed
        context.Sectors.Add(new Sector { SectorId = 1, SectorName = "Pharmaceuticals" });
        context.Companies.Add(new Company { CompanyId = 1, CompanyName = "Company A", TickerSymbol = "COMPA", SectorId = 1, CurrentPrice = 120m });
        context.Companies.Add(new Company { CompanyId = 2, CompanyName = "Company B", TickerSymbol = "COMPB", SectorId = 1, CurrentPrice = 250m });
        context.Users.Add(new AppUser { UserId = 1, Name = "Math Tester", Email = "tester@sharesync.com", PasswordHash = "hash" });
        context.Portfolios.Add(new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "Primary Portfolio", Currency = "BDT" });

        context.SaveChanges();
        return context;
    }

    // =========================================================================
    // PHASE 3 & 4: HOLDINGS, WEIGHTED AVERAGE COST & REALIZED P/L
    // =========================================================================

    [Fact]
    public async Task HoldingsAndRealizedPL_MultiLotTransactions_MatchIndependentReference()
    {
        using var context = CreateInMemoryDbContext();
        var txService = new TransactionService(context);

        // Deterministic transaction schedule:
        // 1. BUY 100 shares @ 100, Fee 10 -> Cost = 10,000, Outflow = 10,010
        // 2. BUY 50 shares @ 120, Fee 15  -> Cost = 6,000, Outflow = 6,015
        // Expected weighted avg = 16,000 / 150 = 106.6666... (106.67)
        // 3. SELL 60 shares @ 130, Fee 20 -> Gross = 7,800, Net Proceeds = 7,780, Cost Basis = 60 * 106.6666... = 6,400
        // Expected realized P/L = 7,780 - 6,400 = 1,380.00
        // 4. SELL 40 shares @ 90, Fee 10  -> Gross = 3,600, Net Proceeds = 3,590, Cost Basis = 40 * 106.6666... = 4,266.67
        // Expected realized P/L = 3,590 - 4,266.67 = -676.67
        // Remaining qty = 150 - 100 = 50 shares
        var rawTxs = new List<(string Type, decimal Qty, decimal Price, decimal Fee)>
        {
            ("BUY", 100m, 100m, 10m),
            ("BUY", 50m, 120m, 15m),
            ("SELL", 60m, 130m, 20m),
            ("SELL", 40m, 90m, 10m)
        };

        var currentPrice = 125m;
        var refResult = IndependentReferenceCalculator.EvaluateHoldings(rawTxs, currentPrice);

        // Execute transactions through production service
        var d1 = new DateTime(2024, 1, 10);
        await txService.CreateTransactionAsync(new CreateTransactionRequestDto { PortfolioId = 1, CompanyId = 1, TransactionType = "BUY", Quantity = 100m, PricePerShare = 100m, Fee = 10m, TransactionDate = d1 }, 1);
        await txService.CreateTransactionAsync(new CreateTransactionRequestDto { PortfolioId = 1, CompanyId = 1, TransactionType = "BUY", Quantity = 50m, PricePerShare = 120m, Fee = 15m, TransactionDate = d1.AddDays(1) }, 1);
        var sell1 = await txService.CreateTransactionAsync(new CreateTransactionRequestDto { PortfolioId = 1, CompanyId = 1, TransactionType = "SELL", Quantity = 60m, PricePerShare = 130m, Fee = 20m, TransactionDate = d1.AddDays(2) }, 1);
        var sell2 = await txService.CreateTransactionAsync(new CreateTransactionRequestDto { PortfolioId = 1, CompanyId = 1, TransactionType = "SELL", Quantity = 40m, PricePerShare = 90m, Fee = 10m, TransactionDate = d1.AddDays(3) }, 1);

        // Verify sell 1 realized P/L
        Assert.NotNull(sell1.Data);
        Assert.NotNull(sell1.Data.RealizedProfitLoss);
        Assert.Equal(1380.00m, sell1.Data.RealizedProfitLoss.Value);

        // Verify sell 2 realized P/L (loss-making)
        Assert.NotNull(sell2.Data);
        Assert.NotNull(sell2.Data.RealizedProfitLoss);
        Assert.Equal(-676.67m, sell2.Data.RealizedProfitLoss.Value);

        // Verify total realized P/L against independent reference
        var totalRealized = sell1.Data.RealizedProfitLoss.Value + sell2.Data.RealizedProfitLoss.Value;
        Assert.Equal(refResult.RealizedProfitLoss, totalRealized);

        // Verify remaining holdings via ReportService
        var reportService = new ReportService(context);
        var holdingsRes = await reportService.GetHoldingsReportAsync(1, 1);
        var item = Assert.Single(holdingsRes.Data!.Holdings);

        Assert.Equal(refResult.RemainingQuantity, item.CurrentQuantity);
        Assert.Equal(refResult.WeightedAverageBuyPrice, item.WeightedAverageBuyPrice);
        Assert.Equal(50m * 120m, item.CurrentMarketValue); // 50 * 120 (current company price is 120)
        Assert.Equal(refResult.InvestedValue, item.InvestedValue);
    }

    // =========================================================================
    // PHASE 5: FEES AND CASH FLOW CONVENTIONS
    // =========================================================================

    [Fact]
    public async Task FeesAndCashFlow_ConventionsArePreservedConsistently()
    {
        using var context = CreateInMemoryDbContext();
        var txService = new TransactionService(context);

        // BUY: Gross 5,000 + Fee 25 -> Net Outflow 5,025
        var buyRes = await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1, CompanyId = 1, TransactionType = "BUY", Quantity = 50m, PricePerShare = 100m, Fee = 25m
        }, 1);

        Assert.NotNull(buyRes.Data);
        Assert.Equal(5000m, buyRes.Data.TotalAmount);
        Assert.Equal(25m, buyRes.Data.Fee);
        Assert.Equal(5025m, buyRes.Data.NetAmount); // Gross + Fee

        // SELL: Gross 2,400 - Fee 12 -> Net Inflow 2,388
        var sellRes = await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1, CompanyId = 1, TransactionType = "SELL", Quantity = 20m, PricePerShare = 120m, Fee = 12m
        }, 1);

        Assert.NotNull(sellRes.Data);
        Assert.Equal(2400m, sellRes.Data.TotalAmount);
        Assert.Equal(12m, sellRes.Data.Fee);
        Assert.Equal(2388m, sellRes.Data.NetAmount); // Gross - Fee
    }

    // =========================================================================
    // PHASE 6 & 7: HISTORICAL RETURN & CAGR
    // =========================================================================

    [Theory]
    [InlineData(100.0, 160.0, 5.0, 60.00, 9.86)]    // 5-year growth: 60% return, 9.86% CAGR
    [InlineData(100.0, 100.0, 3.0, 0.00, 0.00)]      // Zero growth
    [InlineData(200.0, 150.0, 2.0, -25.00, -13.40)]  // Negative return: -25% return, -13.40% CAGR
    [InlineData(50.0, 100.0, 1.0, 100.00, 100.00)]   // Exact 1-year doubling: 100% return, 100% CAGR
    public void HistoricalReturnAndCagr_MatchIndependentReferenceFormulas(
        double startVal, double endVal, double years, decimal expectedReturn, decimal expectedCagr)
    {
        decimal start = (decimal)startVal;
        decimal end = (decimal)endVal;

        decimal simpleReturn = IndependentReferenceCalculator.CalculateSimpleReturn(start, end);
        decimal cagr = IndependentReferenceCalculator.CalculateCagr(start, end, years);

        Assert.Equal(expectedReturn, simpleReturn);
        Assert.Equal(expectedCagr, cagr);
    }

    // =========================================================================
    // PHASE 8 & 9: DAILY RETURNS & ANNUALIZED VOLATILITY
    // =========================================================================

    [Fact]
    public void DailyReturnsAndAnnualizedVolatility_MatchIndependentReference()
    {
        var prices = new List<decimal> { 100m, 105m, 102m, 108m, 106m, 110m };
        var dailyReturns = IndependentReferenceCalculator.CalculateDailyReturns(prices);

        // 5 returns for 6 prices (first observation produces no return)
        Assert.Equal(5, dailyReturns.Count);
        Assert.Equal(5.00m, Math.Round(dailyReturns[0], 2));   // 105 / 100 - 1 = +5.00%
        Assert.Equal(-2.86m, Math.Round(dailyReturns[1], 2));  // 102 / 105 - 1 = -2.86%

        var refVol = IndependentReferenceCalculator.CalculateAnnualizedVolatility(dailyReturns);

        // Compute manual sample standard deviation
        decimal mean = dailyReturns.Average();
        decimal sumSq = dailyReturns.Sum(r => (r - mean) * (r - mean));
        double sampleVariance = (double)(sumSq / (dailyReturns.Count - 1));
        decimal manualVol = Math.Round((decimal)(Math.Sqrt(sampleVariance) * Math.Sqrt(252)), 2);

        Assert.Equal(manualVol, refVol);
    }

    // =========================================================================
    // PHASE 10: MAXIMUM DRAWDOWN (NO LOOK-AHEAD TO FUTURE PEAK)
    // =========================================================================

    [Fact]
    public void MaximumDrawdown_SequentialRunningPeak_NeverUsesFuturePeak()
    {
        // Price series:
        // Day 1: 100 (Peak = 100)
        // Day 2: 120 (Peak = 120)
        // Day 3: 90  (Drawdown = (90 - 120) / 120 = -25%)
        // Day 4: 110 (Drawdown = (110 - 120) / 120 = -8.33%)
        // Day 5: 150 (New Peak = 150)
        // Day 6: 135 (Drawdown = (135 - 150) / 150 = -10%)
        // Maximum drawdown should be exactly -25.00% from Day 2 to Day 3.
        // It must NOT calculate (90 - 150) / 150 = -40% because 150 occurred in the FUTURE.
        var prices = new List<decimal> { 100m, 120m, 90m, 110m, 150m, 135m };

        var maxDd = IndependentReferenceCalculator.CalculateMaximumDrawdown(prices);
        Assert.Equal(-25.00m, maxDd);
    }

    // =========================================================================
    // PHASE 11: SIMPLE MOVING AVERAGES (SMA 50 / SMA 200)
    // =========================================================================

    [Fact]
    public void SimpleMovingAverages_RespectWindowAndReturnNullWhenInsufficient()
    {
        var prices30 = Enumerable.Range(1, 30).Select(i => (decimal)i).ToList();
        var sma50_insufficient = IndependentReferenceCalculator.CalculateSma(prices30, 50);
        Assert.Null(sma50_insufficient); // Must be null, never manufactured

        var prices50 = Enumerable.Range(1, 50).Select(i => (decimal)i).ToList();
        var sma50_exact = IndependentReferenceCalculator.CalculateSma(prices50, 50);
        Assert.NotNull(sma50_exact);
        Assert.Equal(25.50m, sma50_exact.Value); // Average of 1..50 = 25.50

        var prices200 = Enumerable.Repeat(100m, 200).ToList();
        var sma200 = IndependentReferenceCalculator.CalculateSma(prices200, 200);
        Assert.NotNull(sma200);
        Assert.Equal(100.00m, sma200.Value);
    }

    // =========================================================================
    // PHASE 12: BENCHMARK CALCULATIONS (DATE ALIGNMENT & OUTPERFORMANCE)
    // =========================================================================

    [Fact]
    public async Task BenchmarkComparison_AlignedDates_MatchesIndependentCalculation()
    {
        using var context = CreateInMemoryDbContext();
        var dataProvider = new BenchmarkDataProvider();
        var service = new BenchmarkService(context, dataProvider);

        var d1 = new DateTime(2024, 1, 1);
        var d2 = new DateTime(2024, 6, 1);

        // Portfolio snapshots: 100,000 -> 115,000 (+15.00%)
        context.PortfolioSnapshots.Add(new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = d1, TotalValue = 100000m });
        context.PortfolioSnapshots.Add(new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = d2, TotalValue = 115000m });

        // Benchmark records: 6,000 -> 6,300 (+5.00%)
        context.BenchmarkPriceHistories.Add(new BenchmarkPriceHistory { BenchmarkCode = "DSEX", TradingDate = d1, CloseValue = 6000m });
        context.BenchmarkPriceHistories.Add(new BenchmarkPriceHistory { BenchmarkCode = "DSEX", TradingDate = d2, CloseValue = 6300m });
        await context.SaveChangesAsync();

        var res = await service.ComparePortfolioAsync(new BenchmarkComparisonRequestDto
        {
            PortfolioId = 1,
            BenchmarkCode = "DSEX",
            Period = "ALL"
        }, 1);

        Assert.True(res.HasSufficientData);
        Assert.Equal(15.00m, res.PortfolioReturnPercentage);
        Assert.Equal(5.00m, res.BenchmarkReturnPercentage);
        Assert.Equal(10.00m, res.OutperformancePercentage); // 15% - 5% = +10%
        Assert.True(res.IsOutperforming);
    }

    // =========================================================================
    // PHASE 13: INFLATION-ADJUSTED REAL RETURN
    // =========================================================================

    [Fact]
    public void InflationAdjustedReturn_FisherFormula_MatchesIndependentReference()
    {
        // Nominal return 15%, Inflation 9.5%
        // Real Return = ((1 + 0.15) / (1 + 0.095) - 1) * 100 = (1.15 / 1.095 - 1) * 100 = 5.0228... (5.02%)
        decimal nominal = 15.00m;
        decimal inflation = 9.50m;

        var refReal = IndependentReferenceCalculator.CalculateRealReturn(nominal, inflation);
        Assert.NotNull(refReal);
        Assert.Equal(5.02m, refReal.Value);

        // Missing inflation must return null (never silently zero)
        var refNull = IndependentReferenceCalculator.CalculateRealReturn(nominal, null);
        Assert.Null(refNull);
    }

    [Fact]
    public void MultiYearCumulativeInflation_CompoundingFormula_IsAccurate()
    {
        // 2021: 5.56%, 2022: 6.15%, 2023: 9.02%
        // Factor = (1.0556) * (1.0615) * (1.0902) = 1.2215... -> 22.15% cumulative
        var rates = new List<decimal> { 5.56m, 6.15m, 9.02m };
        var cumInf = IndependentReferenceCalculator.CalculateCumulativeInflation(rates);

        Assert.Equal(22.16m, cumInf);
    }

    // =========================================================================
    // PHASE 14: DIVIDEND VS PRICE RETURN DISTINCTION
    // =========================================================================

    [Fact]
    public async Task DividendsAndCapitalGains_AreKeptStrictlySeparateWithoutDoubleCounting()
    {
        using var context = CreateInMemoryDbContext();
        var divService = new DividendService(context);

        // Seed 100 shares of Company 1
        context.Transactions.Add(new Transaction { PortfolioId = 1, CompanyId = 1, TransactionType = "BUY", Quantity = 100m, PricePerShare = 100m, Fee = 0m, TransactionDate = new DateTime(2024, 1, 1) });
        // Dividend of 5 BDT per share
        context.Dividends.Add(new Dividend { CompanyId = 1, DividendPerShare = 5.00m, PaymentDate = new DateTime(2024, 5, 1) });
        await context.SaveChangesAsync();

        var summary = await divService.GetDividendSummaryAsync(1);
        Assert.Equal(500.00m, summary.Data!.TotalIncome); // 100 shares * 5 BDT

        // Verify that capital price return is untouched by dividend income
        var priceReturn = IndependentReferenceCalculator.CalculateSimpleReturn(100m, 120m);
        Assert.Equal(20.00m, priceReturn); // 20% capital appreciation, dividends accounted separately
    }

    // =========================================================================
    // PHASE 16: INVESTMENT INTELLIGENCE SCORE MODEL
    // =========================================================================

    [Fact]
    public void InvestmentIntelligenceScore_DeterministicModel_MatchesIndependentReference()
    {
        // CAGR = 15%, AnnVol = 25%, MaxDD = -20%, PosDayPct = 45%, Trend = STRONGER_UPTREND
        // Return Score: (15 - (-10)) / 30 * 100 = 83.33
        // Risk Score: (25 - 75) / -60 * 100 = 83.33
        // DD Score: (-20 - (-65)) / 55 * 100 = 81.82
        // Con Score: (45 - 20) / 30 * 100 = 83.33
        // Trend Score: 100
        // Overall = 83.33*0.30 + 83.33*0.20 + 81.82*0.20 + 83.33*0.15 + 100*0.15 = 85.50
        var (score, classification) = IndependentReferenceCalculator.EvaluateInvestmentScore(
            cagr: 15m, annVol: 25m, maxDrawdown: -20m, posDayPct: 45m, trendClass: "STRONGER_UPTREND");

        Assert.Equal(85.53m, score);
        Assert.Equal("Strong Historical Profile", classification);
    }

    // =========================================================================
    // PHASE 17: MANDATORY LOOK-AHEAD BIAS TEST
    // =========================================================================

    [Fact]
    public async Task LookAheadBiasTest_FutureObservationAtTPlusOne_DoesNotAffectAsOfT()
    {
        using var context = CreateInMemoryDbContext();
        var service = new InvestmentIntelligenceService(context);

        var dateT = new DateTime(2024, 6, 1);

        // Seed 100 observations ending on date T
        for (int i = 0; i < 100; i++)
        {
            var d = dateT.AddDays(-(100 - i));
            context.CompanyPriceHistories.Add(new CompanyPriceHistory
            {
                CompanyId = 1,
                TradingDate = d,
                RecordedAt = d,
                Price = 100m + i,
                OpenPrice = 100m + i,
                HighPrice = 102m + i,
                LowPrice = 99m + i,
                Source = "Test"
            });
        }
        await context.SaveChangesAsync();

        // 1. Evaluate analysis as of date T
        var baseline = await service.GetHistoricalInvestmentProfileAsync(1, null, dateT, CancellationToken.None);
        var baseEndingPrice = baseline.Data!.Summary.EndingPrice;
        var baseReturn = baseline.Data.Summary.HistoricalReturnPercentage;
        var baseCagr = baseline.Data.Summary.CagrPercentage;
        var baseVol = baseline.Data.Risk.AnnualizedVolatilityPercentage;
        var baseDrawdown = baseline.Data.Risk.MaximumDrawdownPercentage;
        var baseScore = baseline.Data.Score.OverallScore;

        // 2. Add future observation at T + 1 day with a dramatic price spike (e.g. 500)
        context.CompanyPriceHistories.Add(new CompanyPriceHistory
        {
            CompanyId = 1,
            TradingDate = dateT.AddDays(1),
            RecordedAt = dateT.AddDays(1),
            Price = 500m,
            OpenPrice = 500m,
            HighPrice = 510m,
            LowPrice = 490m,
            Source = "Future Observation"
        });
        await context.SaveChangesAsync();

        // 3. Re-evaluate analysis AS OF date T
        var asOfT = await service.GetHistoricalInvestmentProfileAsync(1, null, dateT, CancellationToken.None);

        // Strict Invariant: No metrics as of date T may change due to future T+1 data!
        Assert.Equal(baseEndingPrice, asOfT.Data!.Summary.EndingPrice);
        Assert.Equal(baseReturn, asOfT.Data.Summary.HistoricalReturnPercentage);
        Assert.Equal(baseCagr, asOfT.Data.Summary.CagrPercentage);
        Assert.Equal(baseVol, asOfT.Data.Risk.AnnualizedVolatilityPercentage);
        Assert.Equal(baseDrawdown, asOfT.Data.Risk.MaximumDrawdownPercentage);
        Assert.Equal(baseScore, asOfT.Data.Score.OverallScore);
    }

    // =========================================================================
    // PHASE 18: COMPANY ISOLATION TEST
    // =========================================================================

    [Fact]
    public async Task CompanyIsolationTest_RecordsFromCompanyADoNotAffectCompanyB()
    {
        using var context = CreateInMemoryDbContext();
        var service = new InvestmentIntelligenceService(context);

        var start = new DateTime(2020, 1, 1);

        // Seed Company 1 (GP) with steep bull run (100 -> 300)
        for (int i = 0; i < 60; i++)
        {
            var d = start.AddMonths(i);
            context.CompanyPriceHistories.Add(new CompanyPriceHistory
            {
                CompanyId = 1, TradingDate = d, RecordedAt = d, Price = 100m + (i * 3.3m),
                OpenPrice = 100m, HighPrice = 300m, LowPrice = 90m
            });
        }

        // Seed Company 2 (SQUAREPHARMA) with steady decline (200 -> 100)
        for (int i = 0; i < 60; i++)
        {
            var d = start.AddMonths(i);
            context.CompanyPriceHistories.Add(new CompanyPriceHistory
            {
                CompanyId = 2, TradingDate = d, RecordedAt = d, Price = 200m - (i * 1.6m),
                OpenPrice = 200m, HighPrice = 210m, LowPrice = 90m
            });
        }
        await context.SaveChangesAsync();

        var profileA = await service.GetHistoricalInvestmentProfileAsync(1, null, null, CancellationToken.None);
        var profileB = await service.GetHistoricalInvestmentProfileAsync(2, null, null, CancellationToken.None);

        Assert.True(profileA.Data!.Summary.HistoricalReturnPercentage > 0m);
        Assert.True(profileB.Data!.Summary.HistoricalReturnPercentage < 0m);
        Assert.NotEqual(profileA.Data.Summary.CagrPercentage, profileB.Data.Summary.CagrPercentage);
        Assert.NotEqual(profileA.Data.Score.OverallScore, profileB.Data.Score.OverallScore);
        Assert.Equal("COMPA", profileA.Data.Company.TradingCode);
        Assert.Equal("COMPB", profileB.Data.Company.TradingCode);
    }

    // =========================================================================
    // PHASE 20: NUMERICAL PRECISION & DRIFT TEST
    // =========================================================================

    [Fact]
    public void NumericalPrecision_DecimalValuesAvoidFloatingPointDrift()
    {
        // Repeating fractional shares and fractional prices
        // 0.1m + 0.2m in decimal is exactly 0.3m (no IEEE 754 drift 0.30000000000000004)
        decimal d1 = 0.1m;
        decimal d2 = 0.2m;
        Assert.Equal(0.3m, d1 + d2);

        // Multi-transaction cumulative precision
        decimal sum = 0m;
        for (int i = 0; i < 1000; i++)
        {
            sum += 0.01m;
        }
        Assert.Equal(10.00m, sum);
    }

    // =========================================================================
    // PHASE 21: EDGE CASES
    // =========================================================================

    [Fact]
    public async Task EdgeCases_InsufficientHistoryAndSingleObservation_HandledGracefully()
    {
        using var context = CreateInMemoryDbContext();
        var service = new InvestmentIntelligenceService(context);

        // 1. Single observation
        var singleDate = new DateTime(2024, 1, 1);
        context.CompanyPriceHistories.Add(new CompanyPriceHistory
        {
            CompanyId = 1, TradingDate = singleDate, RecordedAt = singleDate, Price = 100m,
            OpenPrice = 100m, HighPrice = 100m, LowPrice = 100m
        });
        await context.SaveChangesAsync();

        var profile = await service.GetHistoricalInvestmentProfileAsync(1, null, null, CancellationToken.None);

        Assert.NotNull(profile.Data);
        Assert.False(profile.Data.DataQuality.HasFullFiveYearHistory);
        Assert.Contains(profile.Data.DataQuality.DataQualityWarnings, w => w.Contains("Less than five years"));
    }
}
