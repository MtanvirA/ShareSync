using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.DTOs.Currencies;
using ShareSync.Application.DTOs.Portfolios;
using ShareSync.Application.DTOs.Transactions;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class InflationAndMultiAssetCurrencyTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new ShareSyncDbContext(options);

        // Seed Users
        var user1 = new AppUser { UserId = 1, Name = "Alice Investor", Email = "alice@sharesync.com", PasswordHash = "hash1" };
        context.Users.Add(user1);

        // Seed Portfolios
        var p1 = new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "BDT Balanced Portfolio", Currency = "BDT" };
        var p2 = new Portfolio { PortfolioId = 2, UserId = 1, PortfolioName = "USD Global Portfolio", Currency = "USD" };
        context.Portfolios.AddRange(p1, p2);

        // Seed Sector
        var sector = new Sector { SectorId = 1, SectorName = "Equities & Funds" };
        context.Sectors.Add(sector);

        // Seed Companies (Multi-asset and multi-currency)
        var cEquity = new Company
        {
            CompanyId = 1,
            CompanyName = "Grameenphone Ltd",
            TickerSymbol = "GP",
            SectorId = 1,
            CurrentPrice = 300.00m,
            AssetType = "EQUITY",
            Currency = "BDT"
        };
        var cEtf = new Company
        {
            CompanyId = 2,
            CompanyName = "DSE 30 Index ETF",
            TickerSymbol = "DSE30ETF",
            SectorId = 1,
            CurrentPrice = 120.00m,
            AssetType = "ETF",
            Currency = "BDT"
        };
        var cMutualFund = new Company
        {
            CompanyId = 3,
            CompanyName = "Grameen One Scheme Two",
            TickerSymbol = "GRAMEEN1MF",
            SectorId = 1,
            CurrentPrice = 18.00m,
            AssetType = "MUTUAL_FUND",
            Currency = "BDT"
        };
        var cCashBdt = new Company
        {
            CompanyId = 4,
            CompanyName = "BDT Liquid Cash Balance",
            TickerSymbol = "CASH-BDT",
            SectorId = 1,
            CurrentPrice = 1.00m,
            AssetType = "CASH",
            Currency = "BDT"
        };
        var cUsdEquity = new Company
        {
            CompanyId = 5,
            CompanyName = "SPDR S&P 500 ETF Trust",
            TickerSymbol = "SPY",
            SectorId = 1,
            CurrentPrice = 500.00m,
            AssetType = "ETF",
            Currency = "USD"
        };
        context.Companies.AddRange(cEquity, cEtf, cMutualFund, cCashBdt, cUsdEquity);

        // Seed Inflation Rates
        context.InflationRates.AddRange(
            new InflationRate { RateId = 1, CountryCode = "BD", Year = 2020, Rate = 5.65m, Source = "BBS" },
            new InflationRate { RateId = 2, CountryCode = "BD", Year = 2021, Rate = 5.56m, Source = "BBS" },
            new InflationRate { RateId = 3, CountryCode = "BD", Year = 2022, Rate = 6.15m, Source = "BBS" },
            new InflationRate { RateId = 4, CountryCode = "BD", Year = 2023, Rate = 9.02m, Source = "BBS" },
            new InflationRate { RateId = 5, CountryCode = "BD", Year = 2024, Rate = 9.73m, Source = "BBS" },
            new InflationRate { RateId = 6, CountryCode = "BD", Year = 2025, Rate = 10.20m, Source = "BBS" }
        );

        // Seed Currency Exchange Rates
        context.CurrencyExchangeRates.AddRange(
            new CurrencyExchangeRate { RateId = 1, FromCurrency = "USD", ToCurrency = "BDT", ExchangeRate = 122.5000m, EffectiveDate = new DateTime(2026, 1, 1), Source = "BB" },
            new CurrencyExchangeRate { RateId = 2, FromCurrency = "BDT", ToCurrency = "USD", ExchangeRate = 0.008163m, EffectiveDate = new DateTime(2026, 1, 1), Source = "BB" }
        );

        context.SaveChanges();
        return context;
    }

    // =========================================================
    // INFLATION TESTS (LF-28)
    // =========================================================

    [Fact]
    public void Inflation_Model_10PercentNominal_5PercentInflation()
    {
        // Formula: Real = ((1 + 0.10) / (1 + 0.05) - 1) * 100 = (1.10 / 1.05 - 1) * 100 = +4.7619% -> 4.76%
        using var context = CreateInMemoryDbContext();
        var inflationService = new InflationService(context);

        var realReturn = inflationService.CalculateRealReturn(10.0m, 5.0m);
        Assert.NotNull(realReturn);
        Assert.Equal(4.76m, realReturn.Value);
    }

    [Fact]
    public void Inflation_Model_ZeroInflation_ReturnsNominal()
    {
        // Formula: Real = ((1 + 0.10) / (1 + 0.00) - 1) * 100 = 10.00%
        using var context = CreateInMemoryDbContext();
        var inflationService = new InflationService(context);

        var realReturn = inflationService.CalculateRealReturn(10.0m, 0.0m);
        Assert.NotNull(realReturn);
        Assert.Equal(10.00m, realReturn.Value);
    }

    [Fact]
    public void Inflation_Model_NegativeInflation_DeflationIncreasesRealReturn()
    {
        // Deflation -2%: Real = ((1 + 0.10) / (1 - 0.02) - 1) * 100 = (1.10 / 0.98 - 1) * 100 = +12.2448% -> 12.24%
        using var context = CreateInMemoryDbContext();
        var inflationService = new InflationService(context);

        var realReturn = inflationService.CalculateRealReturn(10.0m, -2.0m);
        Assert.NotNull(realReturn);
        Assert.Equal(12.24m, realReturn.Value);
    }

    [Fact]
    public async Task Inflation_MissingPeriod_DoesNotSilentlyAssumeZeroOrInvent()
    {
        // Request years 2010 to 2015 which do NOT exist in BD database or static rates
        using var context = CreateInMemoryDbContext();
        var inflationService = new InflationService(context);

        var result = await inflationService.CalculateRealReturnForPeriodAsync("BD", 15.0m, 2010, 2015);

        Assert.False(result.IsDataAvailable);
        Assert.Null(result.InflationRatePercentage);
        Assert.Null(result.RealReturnPercentage);
        Assert.Contains("unavailable", result.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Inflation_MultiYearCompounding_CalculatesAccurateCumulativeInflation()
    {
        // 2023: 9.02%, 2024: 9.73%
        // Cumulative factor = (1 + 0.0902) * (1 + 0.0973) = 1.0902 * 1.0973 = 1.19627646 -> 19.63%
        using var context = CreateInMemoryDbContext();
        var inflationService = new InflationService(context);

        var cumulRate = await inflationService.GetCumulativeInflationRateAsync("BD", 2023, 2024);

        Assert.NotNull(cumulRate);
        Assert.Equal(19.63m, cumulRate.Value);

        // Nominal return 25%: Real Return = ((1 + 0.25) / (1 + 0.1963) - 1) * 100 = 4.49%
        var realReturn = inflationService.CalculateRealReturn(25.0m, cumulRate.Value);
        Assert.NotNull(realReturn);
        Assert.Equal(4.49m, realReturn.Value);
    }

    // =========================================================
    // MULTI-ASSET TESTS (LF-29)
    // =========================================================

    [Fact]
    public async Task MultiAsset_SupportsEquity_ETF_MutualFund_AndCash()
    {
        using var context = CreateInMemoryDbContext();
        var txService = new TransactionService(context);
        var portfolioService = new PortfolioService(context);

        // 1. Buy Equity (GP)
        await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 10,
            PricePerShare = 280.00m,
            Fee = 10.00m,
            TransactionDate = new DateTime(2025, 1, 10)
        }, userId: 1);

        // 2. Buy ETF (DSE30ETF)
        await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 2,
            TransactionType = "BUY",
            Quantity = 50,
            PricePerShare = 100.00m,
            Fee = 5.00m,
            TransactionDate = new DateTime(2025, 1, 11)
        }, userId: 1);

        // 3. Buy Mutual Fund (GRAMEEN1MF)
        await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 3,
            TransactionType = "BUY",
            Quantity = 200,
            PricePerShare = 15.00m,
            Fee = 2.00m,
            TransactionDate = new DateTime(2025, 1, 12)
        }, userId: 1);

        // 4. Buy Cash (CASH-BDT)
        await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 4,
            TransactionType = "BUY",
            Quantity = 5000,
            PricePerShare = 1.00m,
            Fee = 0.00m,
            TransactionDate = new DateTime(2025, 1, 13)
        }, userId: 1);

        var portfolioDetail = await portfolioService.GetPortfolioByIdAsync(1, 1);
        var holdings = portfolioDetail.Data!.Holdings;

        Assert.Equal(4, holdings.Count);
        Assert.Contains(holdings, h => h.AssetType == "EQUITY" && h.TickerSymbol == "GP");
        Assert.Contains(holdings, h => h.AssetType == "ETF" && h.TickerSymbol == "DSE30ETF");
        Assert.Contains(holdings, h => h.AssetType == "MUTUAL_FUND" && h.TickerSymbol == "GRAMEEN1MF");
        Assert.Contains(holdings, h => h.AssetType == "CASH" && h.TickerSymbol == "CASH-BDT");
    }

    [Fact]
    public async Task MultiAsset_AnalyticsBreaksDownAssetClassAllocation()
    {
        using var context = CreateInMemoryDbContext();
        var txService = new TransactionService(context);
        var reportService = new ReportService(context);
        var dividendService = new DividendService(context);
        var inflationService = new InflationService(context);
        var currencyService = new CurrencyService(context);
        var analyticsService = new AnalyticsService(context, reportService, dividendService, inflationService, currencyService);

        // Buy Equity: 10 * 300 = 3000
        await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 10,
            PricePerShare = 300.00m,
            Fee = 0m,
            TransactionDate = new DateTime(2025, 1, 10)
        }, userId: 1);

        // Buy ETF: 50 * 120 = 6000
        await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 2,
            TransactionType = "BUY",
            Quantity = 50,
            PricePerShare = 120.00m,
            Fee = 0m,
            TransactionDate = new DateTime(2025, 1, 11)
        }, userId: 1);

        // Buy Cash: 1000 * 1 = 1000
        await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 4,
            TransactionType = "BUY",
            Quantity = 1000,
            PricePerShare = 1.00m,
            Fee = 0m,
            TransactionDate = new DateTime(2025, 1, 12)
        }, userId: 1);

        // Total Value = 3000 + 6000 + 1000 = 10,000
        var analyticsRes = await analyticsService.GetAnalyticsAsync(1, 1);
        var assetAlloc = analyticsRes.Data!.AssetTypeAllocation;

        Assert.NotEmpty(assetAlloc);
        var equityAlloc = assetAlloc.FirstOrDefault(a => a.AssetType == "EQUITY");
        var etfAlloc = assetAlloc.FirstOrDefault(a => a.AssetType == "ETF");
        var cashAlloc = assetAlloc.FirstOrDefault(a => a.AssetType == "CASH");

        Assert.NotNull(equityAlloc);
        Assert.NotNull(etfAlloc);
        Assert.NotNull(cashAlloc);

        Assert.Equal(30.0m, equityAlloc.Percentage);
        Assert.Equal(60.0m, etfAlloc.Percentage);
        Assert.Equal(10.0m, cashAlloc.Percentage);
    }

    // =========================================================
    // MULTI-CURRENCY TESTS (LF-29)
    // =========================================================

    [Fact]
    public async Task MultiCurrency_SameCurrencyReturnsParityRate()
    {
        using var context = CreateInMemoryDbContext();
        var currencyService = new CurrencyService(context);

        var bdtRate = await currencyService.GetExchangeRateAsync("BDT", "BDT");
        var usdRate = await currencyService.GetExchangeRateAsync("USD", "USD");

        Assert.Equal(1.0000m, bdtRate);
        Assert.Equal(1.0000m, usdRate);
    }

    [Fact]
    public async Task MultiCurrency_ConvertsCorrectlyWithExchangeRate()
    {
        using var context = CreateInMemoryDbContext();
        var currencyService = new CurrencyService(context);

        // 100 USD to BDT at 122.50 = 12,250 BDT
        var bdtAmount = await currencyService.ConvertAsync(100m, "USD", "BDT");
        Assert.Equal(12250.00m, bdtAmount);

        var details = await currencyService.ConvertWithDetailsAsync(100m, "USD", "BDT");
        Assert.Equal(100m, details.OriginalAmount);
        Assert.Equal(12250.00m, details.ConvertedAmount);
        Assert.Equal(122.5000m, details.ExchangeRate);
        Assert.Equal("USD", details.FromCurrency);
        Assert.Equal("BDT", details.ToCurrency);
    }

    [Fact]
    public async Task MultiCurrency_InvalidCurrencyThrowsAppException()
    {
        using var context = CreateInMemoryDbContext();
        var currencyService = new CurrencyService(context);

        await Assert.ThrowsAsync<AppException>(() => currencyService.GetExchangeRateAsync("EUR", "BDT"));
        await Assert.ThrowsAsync<AppException>(() => currencyService.GetExchangeRateAsync("BDT", "GBP"));
    }

    [Fact]
    public async Task MultiCurrency_PortfolioValuationConvertsAssetForeignCurrencyToBaseCurrency()
    {
        using var context = CreateInMemoryDbContext();
        var txService = new TransactionService(context);
        var portfolioService = new PortfolioService(context);

        // Portfolio 1 is BDT base.
        // Buy US Asset SPY (priced in USD at $500).
        // 2 shares * $500 = $1,000 Local Market Value.
        // Exchange Rate 1 USD = 122.50 BDT.
        // Base Market Value = $1,000 * 122.50 = ৳122,500 BDT.
        await txService.CreateTransactionAsync(new CreateTransactionRequestDto
        {
            PortfolioId = 1,
            CompanyId = 5,
            TransactionType = "BUY",
            Quantity = 2,
            PricePerShare = 500.00m,
            Fee = 0m,
            TransactionDate = new DateTime(2025, 2, 1)
        }, userId: 1);

        var detail = await portfolioService.GetPortfolioByIdAsync(1, 1);
        var holding = Assert.Single(detail.Data!.Holdings);

        Assert.Equal("USD", holding.Currency);
        Assert.Equal(1000.00m, holding.LocalMarketValue);
        Assert.Equal(122500.00m, holding.BaseMarketValue);
        Assert.Equal(122500.00m, holding.MarketValue);
        Assert.Equal(122500.00m, detail.Data.TotalValue);
    }

    // =========================================================
    // BACKWARD COMPATIBILITY TESTS
    // =========================================================

    [Fact]
    public async Task BackwardCompatibility_DefaultEquitiesDefaultToEquityAndBDT()
    {
        using var context = CreateInMemoryDbContext();
        var compService = new CompanyService(context);

        var res = await compService.GetCompanyByIdAsync(1);
        Assert.NotNull(res.Data);
        Assert.Equal("EQUITY", res.Data.AssetType);
        Assert.Equal("BDT", res.Data.Currency);
    }

    [Fact]
    public async Task BackwardCompatibility_DefaultPortfolioDefaultsToBDT()
    {
        using var context = CreateInMemoryDbContext();
        var portfolioService = new PortfolioService(context);

        var p = await portfolioService.CreatePortfolioAsync(new CreatePortfolioRequestDto
        {
            PortfolioName = "Legacy Portfolio",
            Description = "Should default to BDT base"
        }, userId: 1);

        Assert.NotNull(p.Data);
        Assert.Equal("BDT", p.Data.Currency);
    }
}
