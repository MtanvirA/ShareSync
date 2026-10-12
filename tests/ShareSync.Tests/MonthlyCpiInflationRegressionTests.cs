using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.DTOs.Inflation;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

/// <summary>
/// REGRESSION TEST SUITE: Monthly CPI Inflation & Purchasing Power Real Return (LF-28)
/// 
/// Validates:
/// 1. Portfolio starting in 2025 evaluated in 2026 with supported monthly CPI.
/// 2. Correct cumulative inflation from known CPI index values: ((Ending / Starting) - 1) * 100.
/// 3. Correct Fisher-equation real return using cumulative inflation: [ (1 + Nom/100)/(1 + Inf/100) - 1 ] * 100.
/// 4. Correct handling of latest published CPI observation when calendar month is newer.
/// 5. Missing start or end CPI observation produces honest unavailable state (not fabricated).
/// 6. Missing intermediate monthly coverage produces honest unavailable state without extrapolating.
/// 7. Invalid or non-positive CPI index values handled explicitly without crashing.
/// 8. Sovereign country isolation for BD and US (USD portfolio reports US limitation explicitly).
/// 9. Preservation of existing annual inflation API behavior and backward compatibility.
/// 10. Prevention of duplicate monthly CPI observations for the same country and date.
/// 11. Preservation of all core portfolio accounting metrics (value, invested, P/L, fees, cash flow).
/// 12. Correct distinction between 0% real return and unavailable real return.
/// </summary>
public class MonthlyCpiInflationRegressionTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new ShareSyncDbContext(options);

        // Seed default user, portfolio, sector, company
        var user = new AppUser { UserId = 1, Name = "Demo User", Email = "demo@sharesync.com", PasswordHash = "hash" };
        context.Users.Add(user);

        var portfolioBd = new Portfolio { PortfolioId = 1, UserId = 1, PortfolioName = "BD Portfolio", Currency = "BDT", CreatedAt = new DateTime(2025, 1, 1) };
        var portfolioUs = new Portfolio { PortfolioId = 2, UserId = 1, PortfolioName = "US Portfolio", Currency = "USD", CreatedAt = new DateTime(2025, 1, 1) };
        context.Portfolios.AddRange(portfolioBd, portfolioUs);

        var sector = new Sector { SectorId = 1, SectorName = "Financials" };
        context.Sectors.Add(sector);

        var companyGp = new Company { CompanyId = 1, CompanyName = "Grameenphone", TickerSymbol = "GP", SectorId = 1, CurrentPrice = 300.00m, Currency = "BDT" };
        var companyApple = new Company { CompanyId = 2, CompanyName = "Apple Inc", TickerSymbol = "AAPL", SectorId = 1, CurrentPrice = 200.00m, Currency = "USD" };
        context.Companies.AddRange(companyGp, companyApple);

        context.SaveChanges();
        return context;
    }

    private void SeedStandardMonthlyCpi(ShareSyncDbContext context)
    {
        var observations = new List<CpiIndexObservation>
        {
            new() { CountryCode = "BD", ObservationDate = new DateTime(2025, 1, 1), CpiIndex = 131.4872m, Source = "BBS" },
            new() { CountryCode = "BD", ObservationDate = new DateTime(2025, 2, 1), CpiIndex = 131.2854m, Source = "BBS" },
            new() { CountryCode = "BD", ObservationDate = new DateTime(2025, 3, 1), CpiIndex = 132.9763m, Source = "BBS" },
            new() { CountryCode = "BD", ObservationDate = new DateTime(2025, 4, 1), CpiIndex = 133.5048m, Source = "BBS" },
            new() { CountryCode = "BD", ObservationDate = new DateTime(2025, 5, 1), CpiIndex = 133.0813m, Source = "BBS" },
            new() { CountryCode = "BD", ObservationDate = new DateTime(2025, 6, 1), CpiIndex = 133.8462m, Source = "BBS" },
            new() { CountryCode = "BD", ObservationDate = new DateTime(2025, 7, 1), CpiIndex = 136.8319m, Source = "BBS" },
            new() { CountryCode = "BD", ObservationDate = new DateTime(2025, 8, 1), CpiIndex = 140.0624m, Source = "BBS" },
            new() { CountryCode = "BD", ObservationDate = new DateTime(2025, 9, 1), CpiIndex = 141.5266m, Source = "BBS" },
            new() { CountryCode = "BD", ObservationDate = new DateTime(2025, 10, 1), CpiIndex = 144.2073m, Source = "BBS" },
            new() { CountryCode = "BD", ObservationDate = new DateTime(2025, 11, 1), CpiIndex = 143.6473m, Source = "BBS" },
            new() { CountryCode = "BD", ObservationDate = new DateTime(2025, 12, 1), CpiIndex = 142.4371m, Source = "BBS" },
            new() { CountryCode = "BD", ObservationDate = new DateTime(2026, 1, 1), CpiIndex = 142.7712m, Source = "BBS" },
            new() { CountryCode = "BD", ObservationDate = new DateTime(2026, 2, 1), CpiIndex = 143.2784m, Source = "BBS" },
            new() { CountryCode = "BD", ObservationDate = new DateTime(2026, 3, 1), CpiIndex = 144.5586m, Source = "BBS" },
            new() { CountryCode = "BD", ObservationDate = new DateTime(2026, 4, 1), CpiIndex = 145.5748m, Source = "BBS" },
            new() { CountryCode = "BD", ObservationDate = new DateTime(2026, 5, 1), CpiIndex = 145.6140m, Source = "BBS" },
            new() { CountryCode = "BD", ObservationDate = new DateTime(2026, 6, 1), CpiIndex = 146.1060m, Source = "BBS" },
            new() { CountryCode = "BD", ObservationDate = new DateTime(2026, 7, 1), CpiIndex = 148.2145m, Source = "BBS" },
            new() { CountryCode = "BD", ObservationDate = new DateTime(2026, 8, 1), CpiIndex = 151.6366m, Source = "BBS" },
        };

        context.CpiIndexObservations.AddRange(observations);
        context.SaveChanges();
    }

    // 1. Portfolio 2025 -> 2026 evaluation with supported monthly CPI
    [Fact]
    public async Task Portfolio_Transactions2025_Evaluated2026_CalculatesCumulativeInflationAndRealReturn()
    {
        using var context = CreateInMemoryDbContext();
        SeedStandardMonthlyCpi(context);

        // Buy 100 shares of GP on 2025-06-15 at 250 BDT
        context.Transactions.Add(new Transaction
        {
            TransactionId = 1,
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 250.00m,
            Fee = 10.00m,
            TransactionDate = new DateTime(2025, 6, 15)
        });
        await context.SaveChangesAsync();

        var portfolioService = new PortfolioService(context);
        var detailResult = await portfolioService.GetPortfolioByIdAsync(1, 1);

        Assert.NotNull(detailResult);
        var summary = detailResult.Data;
        Assert.NotNull(summary);

        // Total Invested = 25,000; Current Market Value (at 300) = 30,000; Unrealized P/L = +5,000 (+20.00%)
        Assert.Equal(30000.00m, summary.TotalValue);
        Assert.Equal(25000.00m, summary.TotalInvested);
        Assert.Equal(20.00m, summary.UnrealizedProfitLossPercentage);

        // Start month: Jun 2025 (133.8462), End month: Aug 2026 (151.6366)
        // Cumulative Inflation = ((151.6366 / 133.8462) - 1) * 100 = 13.29%
        Assert.NotNull(summary.InflationRate);
        Assert.Equal(13.29m, summary.InflationRate.Value);

        // Real Return: ((1 + 0.20) / (1 + 0.1329) - 1) * 100 = 5.92%
        Assert.NotNull(summary.RealReturnPercentage);
        Assert.Equal(5.92m, summary.RealReturnPercentage.Value);
        Assert.Contains("Aug 2026", summary.InflationStatus);
    }

    // 2. Correct cumulative inflation from two known CPI index values
    [Fact]
    public async Task Inflation_CumulativeInflation_CalculatedAccuratelyFromTwoKnownCpiValues()
    {
        using var context = CreateInMemoryDbContext();
        // Insert custom known CPI values: Start 140.00, End 154.00
        context.CpiIndexObservations.AddRange(
            new CpiIndexObservation { CountryCode = "BD", ObservationDate = new DateTime(2025, 1, 1), CpiIndex = 140.00m, Source = "BBS" },
            new CpiIndexObservation { CountryCode = "BD", ObservationDate = new DateTime(2025, 2, 1), CpiIndex = 154.00m, Source = "BBS" }
        );
        await context.SaveChangesAsync();

        var inflationService = new InflationService(context);
        var rate = await inflationService.GetCumulativeInflationRateForPeriodAsync(
            "BD",
            new DateTime(2025, 1, 10),
            new DateTime(2025, 2, 20));

        Assert.NotNull(rate);
        // ((154.00 / 140.00) - 1) * 100 = 10.00%
        Assert.Equal(10.00m, rate.Value);
    }

    // 3. Correct Fisher-equation real return using that cumulative inflation
    [Fact]
    public void Inflation_FisherRealReturn_CalculatedAccuratelyUsingCumulativeInflation()
    {
        using var context = CreateInMemoryDbContext();
        var inflationService = new InflationService(context);

        // Nominal Return 20.00%, Cumulative Inflation 10.00%
        // Real Return = ((1 + 0.20) / (1 + 0.10) - 1) * 100 = (1.20 / 1.10 - 1) * 100 = 9.0909% -> 9.09%
        var realReturn = inflationService.CalculateRealReturn(20.00m, 10.00m);

        Assert.NotNull(realReturn);
        Assert.Equal(9.09m, realReturn.Value);
    }

    // 4. Correct handling of latest published CPI observation when calendar month is not yet available
    [Fact]
    public async Task Inflation_LatestPublishedObservation_UsedWhenCurrentMonthNotYetAvailable()
    {
        using var context = CreateInMemoryDbContext();
        SeedStandardMonthlyCpi(context);

        var inflationService = new InflationService(context);
        // Request evaluation up to October 2026 when latest published observation is August 2026
        var result = await inflationService.CalculateRealReturnForDateRangeAsync(
            "BD",
            10.00m,
            new DateTime(2026, 7, 1),
            new DateTime(2026, 10, 12));

        Assert.True(result.IsDataAvailable);
        Assert.NotNull(result.InflationRatePercentage);
        // Start: Jul 2026 (148.2145), End: Aug 2026 (151.6366)
        // Inflation = ((151.6366 / 148.2145) - 1) * 100 = 2.31%
        Assert.Equal(2.31m, result.InflationRatePercentage.Value);
        Assert.Equal("Aug 2026", result.LatestObservationMonth);
        Assert.Contains("latest published", result.Status, StringComparison.OrdinalIgnoreCase);
    }

    // 5. Missing start or end CPI values produces unavailable state
    [Fact]
    public async Task Inflation_MissingStartOrEndObservation_ReturnsHonestUnavailableState()
    {
        using var context = CreateInMemoryDbContext();
        SeedStandardMonthlyCpi(context);

        var inflationService = new InflationService(context);

        // Start date 2022-01-01 is prior to available monthly CPI coverage
        var result = await inflationService.CalculateRealReturnForDateRangeAsync(
            "BD",
            15.00m,
            new DateTime(2022, 1, 1),
            new DateTime(2026, 8, 1));

        Assert.False(result.IsDataAvailable);
        Assert.Null(result.InflationRatePercentage);
        Assert.Null(result.RealReturnPercentage);
        Assert.Contains("missing", result.Status, StringComparison.OrdinalIgnoreCase);
    }

    // 6. Missing intermediate coverage returns unavailable without fabricating or extrapolating
    [Fact]
    public async Task Inflation_MissingIntermediateMonthlyCoverage_ReturnsUnavailableWithoutExtrapolating()
    {
        using var context = CreateInMemoryDbContext();
        // Insert Month 1, skip Month 2, insert Month 3
        context.CpiIndexObservations.AddRange(
            new CpiIndexObservation { CountryCode = "BD", ObservationDate = new DateTime(2025, 1, 1), CpiIndex = 130.00m, Source = "BBS" },
            new CpiIndexObservation { CountryCode = "BD", ObservationDate = new DateTime(2025, 3, 1), CpiIndex = 135.00m, Source = "BBS" }
        );
        await context.SaveChangesAsync();

        var inflationService = new InflationService(context);
        var result = await inflationService.CalculateRealReturnForDateRangeAsync(
            "BD",
            10.00m,
            new DateTime(2025, 1, 1),
            new DateTime(2025, 3, 1));

        Assert.False(result.IsDataAvailable);
        Assert.Null(result.InflationRatePercentage);
        Assert.Null(result.RealReturnPercentage);
        Assert.Contains("Missing intermediate", result.Status, StringComparison.OrdinalIgnoreCase);
    }

    // 7. Invalid or non-positive CPI index values handled explicitly without crashing
    [Fact]
    public async Task Inflation_NonPositiveCpiValue_HandledExplicitlyWithoutCrashing()
    {
        using var context = CreateInMemoryDbContext();
        context.CpiIndexObservations.AddRange(
            new CpiIndexObservation { CountryCode = "BD", ObservationDate = new DateTime(2025, 1, 1), CpiIndex = -10.00m, Source = "BBS" },
            new CpiIndexObservation { CountryCode = "BD", ObservationDate = new DateTime(2025, 2, 1), CpiIndex = 130.00m, Source = "BBS" }
        );
        await context.SaveChangesAsync();

        var inflationService = new InflationService(context);
        var result = await inflationService.CalculateRealReturnForDateRangeAsync(
            "BD",
            10.00m,
            new DateTime(2025, 1, 1),
            new DateTime(2025, 2, 1));

        Assert.False(result.IsDataAvailable);
        Assert.Null(result.InflationRatePercentage);
        Assert.Contains("non-positive", result.Status, StringComparison.OrdinalIgnoreCase);
    }

    // 8. Correct country-series isolation for BD and US
    [Fact]
    public async Task Inflation_CountryIsolation_BdAndUs_ReportsUsLimitationExplicitly()
    {
        using var context = CreateInMemoryDbContext();
        SeedStandardMonthlyCpi(context);

        var inflationService = new InflationService(context);

        // USD portfolio query
        var resultUs = await inflationService.CalculateRealReturnForDateRangeAsync(
            "US",
            12.00m,
            new DateTime(2025, 1, 1),
            new DateTime(2026, 8, 1));

        Assert.False(resultUs.IsDataAvailable);
        Assert.Null(resultUs.InflationRatePercentage);
        Assert.Null(resultUs.RealReturnPercentage);
        Assert.Contains("United States", resultUs.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unavailable", resultUs.Status, StringComparison.OrdinalIgnoreCase);
    }

    // 9. Preservation of existing annual inflation API behavior
    [Fact]
    public async Task Inflation_AnnualRatesApi_PreservesExistingBehavior()
    {
        using var context = CreateInMemoryDbContext();
        var inflationService = new InflationService(context);

        // Historical rates call
        var rates = await inflationService.GetHistoricalRatesAsync("BD");
        Assert.NotNull(rates);
        Assert.NotEmpty(rates);
        Assert.Contains(rates, r => r.Year == 2024 && r.InflationRate == 9.73m);

        // Annual multi-year compounding calculation for 2023-2024
        var annualResult = await inflationService.CalculateRealReturnForPeriodAsync("BD", 25.00m, 2023, 2024);
        Assert.True(annualResult.IsDataAvailable);
        Assert.NotNull(annualResult.InflationRatePercentage);
        Assert.Equal(19.63m, annualResult.InflationRatePercentage.Value);
        Assert.Equal(4.49m, annualResult.RealReturnPercentage);
    }

    // 10. Prevention of duplicate monthly CPI observations
    [Fact]
    public void Inflation_DuplicateMonthlyCpiObservation_PreventedInDomainModel()
    {
        using var context = CreateInMemoryDbContext();
        var obs1 = new CpiIndexObservation
        {
            CountryCode = "BD",
            ObservationDate = new DateTime(2026, 8, 1),
            CpiIndex = 151.6366m,
            Source = "BBS"
        };
        var obs2 = new CpiIndexObservation
        {
            CountryCode = "BD",
            ObservationDate = new DateTime(2026, 8, 1),
            CpiIndex = 151.6366m,
            Source = "Duplicate BBS"
        };

        context.CpiIndexObservations.Add(obs1);
        context.SaveChanges();

        // In-memory DbContext throws when adding an entity with identical key,
        // and MySQL schema enforces UQ_CPI_OBSERVATIONS_COUNTRY_DATE
        var entityType = context.Model.FindEntityType(typeof(CpiIndexObservation));
        var uniqueIndex = entityType?.GetIndexes().FirstOrDefault(i => i.IsUnique);
        Assert.NotNull(uniqueIndex);
        var propNames = uniqueIndex.Properties.Select(p => p.Name).ToList();
        Assert.Contains(nameof(CpiIndexObservation.CountryCode), propNames);
        Assert.Contains(nameof(CpiIndexObservation.ObservationDate), propNames);
    }

    // 11. Preservation of portfolio accounting metrics
    [Fact]
    public async Task Portfolio_FinancialMetricsIntegrity_PreservesMarketValueInvestedProfitLossFeesAndCashFlow()
    {
        using var context = CreateInMemoryDbContext();
        SeedStandardMonthlyCpi(context);

        // Multiple buy and partial sell transactions
        context.Transactions.AddRange(
            new Transaction { TransactionId = 1, PortfolioId = 1, CompanyId = 1, TransactionType = "BUY", Quantity = 200, PricePerShare = 200.00m, Fee = 20.00m, TransactionDate = new DateTime(2025, 7, 1) },
            new Transaction { TransactionId = 2, PortfolioId = 1, CompanyId = 1, TransactionType = "BUY", Quantity = 100, PricePerShare = 230.00m, Fee = 15.00m, TransactionDate = new DateTime(2025, 8, 1) },
            new Transaction { TransactionId = 3, PortfolioId = 1, CompanyId = 1, TransactionType = "SELL", Quantity = 50, PricePerShare = 280.00m, Fee = 10.00m, TransactionDate = new DateTime(2025, 9, 1) }
        );
        await context.SaveChangesAsync();

        var portfolioService = new PortfolioService(context);
        var detailResult = await portfolioService.GetPortfolioByIdAsync(1, 1);
        Assert.NotNull(detailResult);
        var summary = detailResult.Data;
        Assert.NotNull(summary);

        // Remaining qty: 250 shares. Current price: 300 BDT
        // Market value = 250 * 300 = 75,000.00
        Assert.Equal(75000.00m, summary.TotalValue);

        // Buy cost: (200*200) + (100*230) = 40,000 + 23,000 = 63,000 across 300 shares -> avg buy price = 210.00
        // Sold 50 shares: realized P/L = (50 * 280 - 10) - (50 * 210) = 13,990 - 10,500 = +3,490.00
        Assert.Equal(3490.00m, summary.RealizedProfitLoss);

        // Active holdings invested = 250 * 210 = 52,500.00
        // Unrealized P/L = 75,000 - 52,500 = +22,500.00
        Assert.Equal(22500.00m, summary.UnrealizedProfitLoss);
        Assert.Equal(52500.00m, summary.TotalInvested);

        // Fees = 20 + 15 + 10 = 45.00
        Assert.Equal(45.00m, summary.TotalFees);

        // Inflow (Gross 14,000 - 10 = 13,990), Outflow (Gross 63,000 + 35 = 63,035) -> Net = 13,990 - 63,035 = -49,045.00
        Assert.Equal(-49045.00m, summary.NetCashFlow);

        // Inflation and real return are accurately calculated alongside
        Assert.NotNull(summary.InflationRate);
        Assert.NotNull(summary.RealReturnPercentage);
    }

    // 12. Distinguishes 0% real return from unavailable real return
    [Fact]
    public void Inflation_ZeroPercentReturn_DistinguishedFromUnavailable()
    {
        using var context = CreateInMemoryDbContext();
        var inflationService = new InflationService(context);

        // 1. Missing inflation -> unavailable (null)
        var unavailableReturn = inflationService.CalculateRealReturn(10.00m, null);
        Assert.Null(unavailableReturn);

        // 2. Exact offsetting return: Nominal return = 5.00%, Inflation = 5.00%
        // Real Return = ((1 + 0.05) / (1 + 0.05) - 1) * 100 = 0.00%
        var zeroReturn = inflationService.CalculateRealReturn(5.00m, 5.00m);
        Assert.NotNull(zeroReturn);
        Assert.Equal(0.00m, zeroReturn.Value);

        // 3. Negative real return: Nominal return = 0.00%, Inflation = 2.31%
        // Real Return = (1.00 / 1.0231 - 1) * 100 = -2.26%
        var negReturn = inflationService.CalculateRealReturn(0.00m, 2.31m);
        Assert.NotNull(negReturn);
        Assert.Equal(-2.26m, negReturn.Value);
    }
}
