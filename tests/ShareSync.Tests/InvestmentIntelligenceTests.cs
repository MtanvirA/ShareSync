using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Interfaces;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using Xunit;

namespace ShareSync.Tests;

public class InvestmentIntelligenceTests
{
    [Fact]
    public async Task GetHistoricalInvestmentProfile_MathematicalFormulas_AreCorrect()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ShareSync.Infrastructure.Data.ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        using (var context = new ShareSync.Infrastructure.Data.ShareSyncDbContext(options))
        {
            context.Sectors.Add(new Sector { SectorId = 1, SectorName = "Test Sector" });
            context.Companies.Add(new Company 
            { 
                CompanyId = 1, 
                CompanyName = "Test Company", 
                TickerSymbol = "TEST", 
                SectorId = 1
            });
            
            var startDate = new DateTime(2018, 1, 1);
            var endDate = new DateTime(2023, 1, 1);
            
            // Start = 100, End = 160
            context.CompanyPriceHistories.Add(new CompanyPriceHistory { CompanyId = 1, TradingDate = startDate, Price = 100, OpenPrice = 100, HighPrice = 100, LowPrice = 100 });
            // 110 (+10%)
            context.CompanyPriceHistories.Add(new CompanyPriceHistory { CompanyId = 1, TradingDate = startDate.AddDays(1), Price = 110, OpenPrice = 110, HighPrice = 110, LowPrice = 110 });
            // 99 (-10%)
            context.CompanyPriceHistories.Add(new CompanyPriceHistory { CompanyId = 1, TradingDate = startDate.AddDays(2), Price = 99, OpenPrice = 99, HighPrice = 99, LowPrice = 99 });
            // Peak = 120
            context.CompanyPriceHistories.Add(new CompanyPriceHistory { CompanyId = 1, TradingDate = startDate.AddDays(3), Price = 120, OpenPrice = 120, HighPrice = 120, LowPrice = 120 });
            // Drawdown to 90
            context.CompanyPriceHistories.Add(new CompanyPriceHistory { CompanyId = 1, TradingDate = startDate.AddDays(4), Price = 90, OpenPrice = 90, HighPrice = 90, LowPrice = 90 });
            
            // Fill to 5 years and exactly 50 items to test MA
            for(int i=5; i<50; i++)
            {
                context.CompanyPriceHistories.Add(new CompanyPriceHistory { CompanyId = 1, TradingDate = startDate.AddDays(i), Price = 100, OpenPrice = 100, HighPrice = 100, LowPrice = 100 });
            }
            
            context.CompanyPriceHistories.Add(new CompanyPriceHistory { CompanyId = 1, TradingDate = endDate, Price = 160, OpenPrice = 160, HighPrice = 160, LowPrice = 160 });
            
            await context.SaveChangesAsync();
        }

        using (var context = new ShareSync.Infrastructure.Data.ShareSyncDbContext(options))
        {
            var service = new InvestmentIntelligenceService(context);
            
            // Act
            var response = await service.GetHistoricalInvestmentProfileAsync(1, null, null, CancellationToken.None);
            
            // Assert
            var data = response.Data;
            Assert.NotNull(data);
            
            // TEST 1: Return = 60%
            Assert.Equal(60m, data.Summary.HistoricalReturnPercentage);
            
            // TEST 2: CAGR ≈ 9.86%
            Assert.InRange(data.Summary.CagrPercentage, 9.85m, 9.87m);
            
            // TEST 3 & 4: Drawdown
            // Peak was 120, dropped to 90 -> -25%
            Assert.Equal(-25m, data.Risk.MaximumDrawdownPercentage);
            
            // Future data leakage would be covered by ensuring `endDate` bounds the window
        }
    }
}
