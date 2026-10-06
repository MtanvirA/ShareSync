using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class DataImporterTests
{
    private ShareSyncDbContext GetDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ShareSyncDbContext>();
        optionsBuilder.UseOracle("Data Source=localhost:1521/FREEPDB1;User Id=sharesync;Password=ShareSync2026#;");
        return new ShareSyncDbContext(optionsBuilder.Options);
    }

    [Fact]
    public async Task HistoricalData_ShouldNotHave_ExactOrConflictingDuplicates()
    {
        using var context = GetDbContext();
        
        var allRecords = await context.CompanyPriceHistories
            .Select(x => new { x.CompanyId, x.TradingDate })
            .ToListAsync();
            
        var duplicates = allRecords
            .GroupBy(x => new { x.CompanyId, x.TradingDate })
            .Where(g => g.Count() > 1)
            .ToList();
            
        Assert.Empty(duplicates);
    }

    [Fact]
    public async Task HistoricalData_ShouldNotHave_InvalidOhlc()
    {
        using var context = GetDbContext();
        
        var invalidRecords = await context.CompanyPriceHistories
            .Where(h => h.HighPrice < h.LowPrice || 
                        h.HighPrice < h.OpenPrice || 
                        h.HighPrice < h.Price || 
                        h.LowPrice > h.OpenPrice || 
                        h.LowPrice > h.Price)
            .ToListAsync();
            
        Assert.Empty(invalidRecords);
    }

    [Fact]
    public async Task HistoricalData_ShouldNotHave_ZeroPrices()
    {
        using var context = GetDbContext();
        
        var zeroPrices = await context.CompanyPriceHistories
            .Where(h => h.OpenPrice == 0 && h.HighPrice == 0 && h.LowPrice == 0 && h.Price == 0)
            .ToListAsync();
            
        Assert.Empty(zeroPrices);
    }

    [Fact]
    public async Task HistoricalData_ShouldHave_CorrectProvenance()
    {
        using var context = GetDbContext();
        
        var anyRecord = await context.CompanyPriceHistories.FirstOrDefaultAsync();
        if (anyRecord != null)
        {
            Assert.Equal("Harvard Dataverse", anyRecord.Source);
            Assert.Equal("Dhaka Stock Exchange Historical Data", anyRecord.SourceDataset);
            Assert.Equal("10.7910/DVN/XIFYT1", anyRecord.SourceDoi);
            Assert.NotNull(anyRecord.ImportBatchId);
        }
    }
}
