using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShareSync.Application.DTOs.Benchmarks;
using ShareSync.Application.DTOs.Companies;
using ShareSync.Application.DTOs.Transactions;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class FeatureExposurePolishTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new ShareSyncDbContext(options);

        // Core User
        var user = new AppUser { UserId = 1, Name = "Test Investor", Email = "investor@sharesync.com", PasswordHash = "hash1", Role = "INVESTOR" };
        context.Users.Add(user);

        // Portfolios
        var p1 = new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "Primary Portfolio", Currency = "BDT" };
        var p2 = new Portfolio { PortfolioId = 2, UserId = 1, PortfolioName = "USD Global Portfolio", Currency = "USD" };
        context.Portfolios.AddRange(p1, p2);

        // Sector
        var sector = new Sector { SectorId = 1, SectorName = "Multi-Asset Market" };
        context.Sectors.Add(sector);

        // Canonical Companies spanning all LF-29 asset types and currencies
        var cEquity = new Company
        {
            CompanyId = 1,
            CompanyName = "Square Pharmaceuticals PLC",
            TickerSymbol = "SQUAREPHARMA",
            SectorId = 1,
            CurrentPrice = 228.40m,
            AssetType = "EQUITY",
            Currency = "BDT"
        };
        var cMutualFund = new Company
        {
            CompanyId = 2,
            CompanyName = "Grameen One Scheme Two Mutual Fund",
            TickerSymbol = "GRAMEEN1MF",
            SectorId = 1,
            CurrentPrice = 18.20m,
            AssetType = "MUTUAL_FUND",
            Currency = "BDT"
        };
        var cEtfBdt = new Company
        {
            CompanyId = 3,
            CompanyName = "DSE 30 Shariah Index ETF",
            TickerSymbol = "DSE30ETF",
            SectorId = 1,
            CurrentPrice = 105.50m,
            AssetType = "ETF",
            Currency = "BDT"
        };
        var cCashBdt = new Company
        {
            CompanyId = 4,
            CompanyName = "BDT Liquid Cash Reserve",
            TickerSymbol = "CASH-BDT",
            SectorId = 1,
            CurrentPrice = 1.00m,
            AssetType = "CASH",
            Currency = "BDT"
        };
        var cEtfUsd = new Company
        {
            CompanyId = 5,
            CompanyName = "SPDR S&P 500 ETF Trust",
            TickerSymbol = "SPY",
            SectorId = 1,
            CurrentPrice = 580.00m,
            AssetType = "ETF",
            Currency = "USD"
        };
        var cCashUsd = new Company
        {
            CompanyId = 6,
            CompanyName = "USD Treasury Cash Reserve",
            TickerSymbol = "CASH-USD",
            SectorId = 1,
            CurrentPrice = 1.00m,
            AssetType = "CASH",
            Currency = "USD"
        };
        context.Companies.AddRange(cEquity, cMutualFund, cEtfBdt, cCashBdt, cEtfUsd, cCashUsd);

        // Exchange Rates
        context.CurrencyExchangeRates.AddRange(
            new CurrencyExchangeRate
            {
                RateId = 1,
                FromCurrency = "USD",
                ToCurrency = "BDT",
                ExchangeRate = 121.50m,
                EffectiveDate = DateTime.UtcNow.Date,
                Source = "Bangladesh Bank Interbank Rate"
            },
            new CurrencyExchangeRate
            {
                RateId = 2,
                FromCurrency = "BDT",
                ToCurrency = "USD",
                ExchangeRate = 0.00823m,
                EffectiveDate = DateTime.UtcNow.Date,
                Source = "Bangladesh Bank Interbank Rate"
            }
        );

        context.SaveChanges();
        return context;
    }

    // =========================================================================
    // LF-29: MULTI-ASSET & MULTI-CURRENCY CANONICAL EXPOSURE & ANTI-SPOOFING
    // =========================================================================

    [Fact]
    public async Task LF29_GetAllCompanies_ExposesCanonicalAssetTypeAndCurrency()
    {
        using var context = CreateInMemoryDbContext();
        var companyService = new CompanyService(context);

        var response = await companyService.GetAllCompaniesAsync();
        Assert.True(response.Success);
        var companies = response.Data!;

        // 1. Equity selection
        var equity = companies.FirstOrDefault(c => c.TickerSymbol == "SQUAREPHARMA");
        Assert.NotNull(equity);
        Assert.Equal("EQUITY", equity.AssetType);
        Assert.Equal("BDT", equity.Currency);

        // 2. Mutual fund selection
        var mf = companies.FirstOrDefault(c => c.TickerSymbol == "GRAMEEN1MF");
        Assert.NotNull(mf);
        Assert.Equal("MUTUAL_FUND", mf.AssetType);
        Assert.Equal("BDT", mf.Currency);

        // 3. ETF selection
        var etfBdt = companies.FirstOrDefault(c => c.TickerSymbol == "DSE30ETF");
        Assert.NotNull(etfBdt);
        Assert.Equal("ETF", etfBdt.AssetType);
        Assert.Equal("BDT", etfBdt.Currency);

        var etfUsd = companies.FirstOrDefault(c => c.TickerSymbol == "SPY");
        Assert.NotNull(etfUsd);
        Assert.Equal("ETF", etfUsd.AssetType);
        Assert.Equal("USD", etfUsd.Currency);

        // 4. Cash selection
        var cashBdt = companies.FirstOrDefault(c => c.TickerSymbol == "CASH-BDT");
        Assert.NotNull(cashBdt);
        Assert.Equal("CASH", cashBdt.AssetType);
        Assert.Equal("BDT", cashBdt.Currency);

        var cashUsd = companies.FirstOrDefault(c => c.TickerSymbol == "CASH-USD");
        Assert.NotNull(cashUsd);
        Assert.Equal("CASH", cashUsd.AssetType);
        Assert.Equal("USD", cashUsd.Currency);
    }

    [Fact]
    public async Task LF29_GetCompanyById_ReturnsCanonicalProperties()
    {
        using var context = CreateInMemoryDbContext();
        var companyService = new CompanyService(context);

        // BDT Mutual Fund
        var resMf = await companyService.GetCompanyByIdAsync(2);
        Assert.True(resMf.Success);
        Assert.Equal("MUTUAL_FUND", resMf.Data!.AssetType);
        Assert.Equal("BDT", resMf.Data!.Currency);

        // USD Cash
        var resUsdCash = await companyService.GetCompanyByIdAsync(6);
        Assert.True(resUsdCash.Success);
        Assert.Equal("CASH", resUsdCash.Data!.AssetType);
        Assert.Equal("USD", resUsdCash.Data!.Currency);
    }

    [Fact]
    public void LF29_CreateTransactionRequestDto_AntiSpoofingEnforced()
    {
        // Verify via reflection that CreateTransactionRequestDto does NOT have AssetType or Currency properties.
        // The browser/client CANNOT pass or override canonical asset type and currency.
        var dtoType = typeof(CreateTransactionRequestDto);
        var assetTypeProp = dtoType.GetProperty("AssetType");
        var currencyProp = dtoType.GetProperty("Currency");

        Assert.Null(assetTypeProp);
        Assert.Null(currencyProp);
    }

    [Fact]
    public async Task LF29_TransactionService_ExecutesWithCanonicalPropertiesWithoutClientOverride()
    {
        using var context = CreateInMemoryDbContext();
        var txService = new TransactionService(context);

        // Record a BUY of USD Cash
        var request = new CreateTransactionRequestDto
        {
            PortfolioId = 2,
            CompanyId = 6, // CASH-USD
            TransactionType = "BUY",
            Quantity = 1000m,
            PricePerShare = 1.00m,
            Fee = 5.00m,
            TransactionDate = DateTime.UtcNow
        };

        var res = await txService.CreateTransactionAsync(request, 1);
        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        Assert.Equal(1000m, res.Data.Quantity);
        Assert.Equal(1.00m, res.Data.PricePerShare);
        Assert.Equal(1000m, res.Data.TotalAmount);
        Assert.Equal(5.00m, res.Data.Fee);
        Assert.Equal(1005m, res.Data.NetAmount);

        // Verify database transaction record and audit
        var dbTx = await context.Transactions.Include(t => t.Company).FirstOrDefaultAsync(t => t.TransactionId == res.Data.TransactionId);
        Assert.NotNull(dbTx);
        Assert.NotNull(dbTx.Company);
        Assert.Equal("CASH", dbTx.Company.AssetType);
        Assert.Equal("USD", dbTx.Company.Currency);

        var audit = await context.TransactionAudits.FirstOrDefaultAsync(a => a.TransactionId == dbTx.TransactionId);
        Assert.NotNull(audit);
        Assert.Contains("CASH-USD", audit.Details);
    }

    [Fact]
    public async Task LF29_CurrencyService_ProvidesExchangeRatesForBaseCurrencyValuation()
    {
        using var context = CreateInMemoryDbContext();
        var currencyService = new CurrencyService(context);

        var rates = await currencyService.GetExchangeRatesAsync();
        Assert.NotEmpty(rates);

        var usdToBdt = rates.FirstOrDefault(r => r.FromCurrency == "USD" && r.ToCurrency == "BDT");
        Assert.NotNull(usdToBdt);
        Assert.Equal(121.50m, usdToBdt.ExchangeRate);

        // Conversion test
        var converted = await currencyService.ConvertAsync(100m, "USD", "BDT");
        Assert.Equal(12150.00m, converted);
    }

    // =========================================================================
    // LF-30: BENCHMARK FALLBACK VISIBILITY & ARITHMETIC INTEGRITY
    // =========================================================================

    [Fact]
    public async Task LF30_RealDatabaseBenchmarkData_IsFallbackIsFalse()
    {
        using var context = CreateInMemoryDbContext();
        var dataProvider = new BenchmarkDataProvider();
        var service = new BenchmarkService(context, dataProvider);

        var d1 = new DateTime(2024, 1, 1);
        var d2 = new DateTime(2024, 1, 15);

        // Seed portfolio snapshots
        context.PortfolioSnapshots.AddRange(
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = d1, TotalValue = 100000m },
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = d2, TotalValue = 110000m }
        );

        // Seed real benchmark database records
        context.BenchmarkPriceHistories.AddRange(
            new BenchmarkPriceHistory
            {
                BenchmarkPriceHistoryId = 1,
                BenchmarkCode = "DSEX",
                BenchmarkName = "DSE Broad Index (DSEX)",
                TradingDate = d1,
                CloseValue = 6000m,
                Source = "Dhaka Stock Exchange Canonical Archive"
            },
            new BenchmarkPriceHistory
            {
                BenchmarkPriceHistoryId = 2,
                BenchmarkCode = "DSEX",
                BenchmarkName = "DSE Broad Index (DSEX)",
                TradingDate = d2,
                CloseValue = 6300m,
                Source = "Dhaka Stock Exchange Canonical Archive"
            }
        );
        await context.SaveChangesAsync();

        var result = await service.ComparePortfolioAsync(new BenchmarkComparisonRequestDto
        {
            PortfolioId = 1,
            BenchmarkCode = "DSEX",
            Period = "ALL"
        }, 1, CancellationToken.None);

        Assert.True(result.HasSufficientData);
        // Requirement: IsFallback MUST be false for database benchmark data (UI hides fallback badge)
        Assert.False(result.IsFallback);
        Assert.Contains("Database Historical Series", result.BenchmarkSource);

        // Mathematical accuracy checks
        Assert.Equal(100000m, result.PortfolioStartingValue);
        Assert.Equal(110000m, result.PortfolioEndingValue);
        Assert.Equal(10.00m, result.PortfolioReturnPercentage); // (110000 - 100000)/100000 = +10%

        Assert.Equal(6000m, result.BenchmarkStartingValue);
        Assert.Equal(6300m, result.BenchmarkEndingValue);
        Assert.Equal(5.00m, result.BenchmarkReturnPercentage); // (6300 - 6000)/6000 = +5%

        Assert.Equal(5.00m, result.OutperformancePercentage); // 10% - 5% = +5%
        Assert.True(result.IsOutperforming);
    }

    [Fact]
    public async Task LF30_FallbackBenchmarkData_IsFallbackIsTrueWithClearNotice()
    {
        using var context = CreateInMemoryDbContext();
        var dataProvider = new BenchmarkDataProvider();
        var service = new BenchmarkService(context, dataProvider);

        var d1 = new DateTime(2024, 1, 1);
        var d2 = new DateTime(2024, 1, 15);

        // Seed portfolio snapshots but NO database benchmark records
        context.PortfolioSnapshots.AddRange(
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = d1, TotalValue = 100000m },
            new PortfolioSnapshot { PortfolioId = 1, SnapshotDate = d2, TotalValue = 110000m }
        );
        await context.SaveChangesAsync();

        var result = await service.ComparePortfolioAsync(new BenchmarkComparisonRequestDto
        {
            PortfolioId = 1,
            BenchmarkCode = "DSEX",
            Period = "ALL"
        }, 1, CancellationToken.None);

        Assert.True(result.HasSufficientData);
        // Requirement: IsFallback MUST be true when fallback provider is active (UI shows fallback badge)
        Assert.True(result.IsFallback);
        Assert.Equal("Static Calibrated Model (Fallback)", result.BenchmarkSource);
        Assert.Contains("Calibrated reference benchmark model used", result.DataLimitationNotice);

        // Arithmetic calculations remain valid and consistent
        Assert.Equal(100000m, result.PortfolioStartingValue);
        Assert.Equal(110000m, result.PortfolioEndingValue);
        Assert.Equal(10.00m, result.PortfolioReturnPercentage);
        Assert.Equal(result.PortfolioReturnPercentage - result.BenchmarkReturnPercentage, result.OutperformancePercentage);
    }
}
