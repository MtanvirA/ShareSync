using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.InvestmentIntelligence;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Services;

public class InvestmentIntelligenceService : IInvestmentIntelligenceService
{
    private readonly IApplicationDbContext _context;

    public InvestmentIntelligenceService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<HistoricalInvestmentProfileDto>> GetHistoricalInvestmentProfileAsync(
        int companyId, 
        DateTime? fromDate = null, 
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var company = await _context.Companies
            .Include(c => c.Sector)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == companyId, cancellationToken);
            
        if (company == null)
            throw new NotFoundException("Company", companyId);

        // Phase 3: REQUIRED RAW ANALYSIS DATA
        var baseQuery = _context.CompanyPriceHistories
            .AsNoTracking()
            .Where(h => h.CompanyId == companyId && h.Price > 0 && (h.TradingDate != null || h.RecordedAt != default));

        if (toDate.HasValue)
            baseQuery = baseQuery.Where(h => (h.TradingDate ?? h.RecordedAt) <= toDate.Value);

        // Phase 2: ANALYSIS WINDOW
        var allHistoryDesc = await baseQuery
            .OrderByDescending(h => h.TradingDate ?? h.RecordedAt)
            .ToListAsync(cancellationToken);

        if (!allHistoryDesc.Any() || allHistoryDesc.Count < 5)
        {
            await SeedFromCsvIfAvailableAsync(companyId, company.TickerSymbol, cancellationToken);
            allHistoryDesc = await baseQuery
                .OrderByDescending(h => h.TradingDate ?? h.RecordedAt)
                .ToListAsync(cancellationToken);
        }

        if (!allHistoryDesc.Any())
            return CreateEmptyProfile(company);

        var latestObservation = allHistoryDesc.First();
        var endDate = latestObservation.TradingDate ?? latestObservation.RecordedAt;
        var nominalStartDate = fromDate ?? endDate.AddYears(-5);
        
        var windowData = allHistoryDesc
            .Where(h => (h.TradingDate ?? h.RecordedAt) >= nominalStartDate)
            .OrderBy(h => h.TradingDate ?? h.RecordedAt)
            .ToList();

        if (!windowData.Any())
            return CreateEmptyProfile(company);
            
        var startDate = windowData.First().TradingDate ?? windowData.First().RecordedAt;
        double years = (endDate - startDate).TotalDays / 365.25;
        bool hasFullFiveYearHistory = years >= 4.7; // allow for calendar gaps in historical trading
        
        var profile = new HistoricalInvestmentProfileDto
        {
            Company = new CompanyInfoDto
            {
                Id = company.CompanyId,
                Name = company.CompanyName,
                TradingCode = company.TickerSymbol,
                Sector = company.Sector?.SectorName ?? "Unknown"
            },
            AnalysisPeriod = new AnalysisPeriodDto
            {
                StartDate = startDate.ToString("yyyy-MM-dd"),
                EndDate = endDate.ToString("yyyy-MM-dd"),
                Years = Math.Round(years, 2),
                HasFullFiveYearHistory = hasFullFiveYearHistory
            }
        };
        
        // Phase 4: HISTORICAL PRICE RETURN
        decimal startingPrice = windowData.First().Price;
        decimal endingPrice = windowData.Last().Price;
        decimal historicalReturn = startingPrice > 0 ? ((endingPrice / startingPrice) - 1) * 100 : 0;
        
        // Phase 5: CAGR
        decimal cagr = 0;
        if (years > 0 && startingPrice > 0 && endingPrice > 0)
        {
            cagr = (decimal)(Math.Pow((double)(endingPrice / startingPrice), 1.0 / years) - 1) * 100;
        }
        
        profile.Summary = new SummaryDto
        {
            StartingPrice = startingPrice,
            EndingPrice = endingPrice,
            HistoricalReturnPercentage = Math.Round(historicalReturn, 2),
            CagrPercentage = Math.Round(cagr, 2)
        };

        // Phase 6, 7, 8, 9: Daily Returns, Volatility, Drawdown, Consistency
        decimal runningPeak = startingPrice;
        decimal maxDrawdown = 0;
        string maxDrawdownDate = (windowData.First().TradingDate ?? windowData.First().RecordedAt).ToString("yyyy-MM-dd");
        
        int positiveDays = 0;
        int negativeDays = 0;
        int flatDays = 0;
        
        List<decimal> dailyReturnsList = new();
        
        // Moving Averages need history BEFORE the window too
        var fullHistoryAsc = allHistoryDesc.OrderBy(h => h.TradingDate ?? h.RecordedAt).ToList();
        
        for (int i = 0; i < windowData.Count; i++)
        {
            var h = windowData[i];
            decimal? dailyReturn = null;
            
            if (i > 0)
            {
                var prevPrice = windowData[i-1].Price;
                if (prevPrice > 0)
                {
                    dailyReturn = ((h.Price / prevPrice) - 1) * 100;
                    dailyReturnsList.Add(dailyReturn.Value);
                    
                    if (dailyReturn > 0) positiveDays++;
                    else if (dailyReturn < 0) negativeDays++;
                    else flatDays++;
                }
            }
            
            if (h.Price > runningPeak) runningPeak = h.Price;
            decimal drawdown = runningPeak > 0 ? ((h.Price - runningPeak) / runningPeak) * 100 : 0;
            if (drawdown < maxDrawdown) 
            {
                maxDrawdown = drawdown;
                maxDrawdownDate = (h.TradingDate ?? h.RecordedAt).ToString("yyyy-MM-dd");
            }
            
            // Calculate MA for chart
            var curDate = h.TradingDate ?? h.RecordedAt;
            var currentIndexInFull = fullHistoryAsc.FindIndex(x => (x.TradingDate ?? x.RecordedAt) == curDate);
            decimal? ma50 = null;
            decimal? ma200 = null;
            
            if (currentIndexInFull >= 49)
            {
                ma50 = fullHistoryAsc.Skip(currentIndexInFull - 49).Take(50).Average(x => x.Price);
            }
            if (currentIndexInFull >= 199)
            {
                ma200 = fullHistoryAsc.Skip(currentIndexInFull - 199).Take(200).Average(x => x.Price);
            }
            
            profile.ChartData.Add(new ChartDataPointDto
            {
                Date = curDate.ToString("yyyy-MM-dd"),
                Close = Math.Round(h.Price, 2),
                Ma50 = ma50.HasValue ? Math.Round(ma50.Value, 2) : null,
                Ma200 = ma200.HasValue ? Math.Round(ma200.Value, 2) : null,
                Drawdown = Math.Round(drawdown, 2)
            });
        }
        
        // Volatility
        decimal dailyVolatility = 0;
        if (dailyReturnsList.Count > 1)
        {
            decimal avgReturn = dailyReturnsList.Average();
            decimal sumSq = dailyReturnsList.Sum(r => (r - avgReturn) * (r - avgReturn));
            dailyVolatility = (decimal)Math.Sqrt((double)(sumSq / (dailyReturnsList.Count - 1)));
        }
        decimal annVolatility = dailyVolatility * (decimal)Math.Sqrt(252);
        
        profile.Risk = new RiskDto
        {
            DailyVolatility = Math.Round(dailyVolatility, 4),
            AnnualizedVolatilityPercentage = Math.Round(annVolatility, 2),
            MaximumDrawdownPercentage = Math.Round(maxDrawdown, 2),
            MaximumDrawdownDate = maxDrawdownDate
        };
        
        // Yearly
        var yearlyGroups = windowData
            .GroupBy(h => (h.TradingDate ?? h.RecordedAt).Year)
            .Select(g => new
            {
                Year = g.Key,
                StartPrice = g.OrderBy(x => x.TradingDate ?? x.RecordedAt).First().Price,
                EndPrice = g.OrderByDescending(x => x.TradingDate ?? x.RecordedAt).First().Price
            })
            .Select(y => new YearlyPerformanceDto
            {
                Year = y.Year,
                StartPrice = y.StartPrice,
                EndPrice = y.EndPrice,
                ReturnPercentage = y.StartPrice > 0 ? Math.Round(((y.EndPrice / y.StartPrice) - 1) * 100, 2) : 0
            })
            .ToList();
            
        profile.YearlyPerformance = yearlyGroups;
        
        int posYears = yearlyGroups.Count(y => y.ReturnPercentage > 0);
        int negYears = yearlyGroups.Count(y => y.ReturnPercentage < 0);
        
        int validReturnDays = positiveDays + negativeDays + flatDays;
        decimal posDayPct = validReturnDays > 0 ? ((decimal)positiveDays / validReturnDays) * 100 : 0;
        
        profile.Consistency = new ConsistencyDto
        {
            PositiveDays = positiveDays,
            NegativeDays = negativeDays,
            FlatDays = flatDays,
            ValidReturnDays = validReturnDays,
            PositiveDayPercentage = Math.Round(posDayPct, 2),
            PositiveYears = posYears,
            NegativeYears = negYears
        };
        
        // Trend
        var lastChartData = profile.ChartData.LastOrDefault();
        decimal? latestMa50 = lastChartData?.Ma50;
        decimal? latestMa200 = lastChartData?.Ma200;
        string trendClass = "TREND_INSUFFICIENT_DATA";
        
        if (latestMa50.HasValue && latestMa200.HasValue)
        {
            if (endingPrice > latestMa50.Value && latestMa50.Value > latestMa200.Value)
                trendClass = "STRONGER_UPTREND";
            else if (endingPrice > latestMa50.Value && latestMa50.Value <= latestMa200.Value)
                trendClass = "UPTREND";
            else if (endingPrice < latestMa50.Value && latestMa50.Value < latestMa200.Value)
                trendClass = "DOWNTREND";
            else
                trendClass = "WEAKENING_OR_MIXED";
        }
        
        profile.Trend = new TrendDto
        {
            LatestClose = endingPrice,
            LatestMA50 = latestMa50,
            LatestMA200 = latestMa200,
            Classification = trendClass
        };
        
        // Score Model calibrated for DSE emerging equity market
        decimal returnScore = NormalizeScore(cagr, -10, 20);
        decimal riskScore = NormalizeScore(annVolatility, 75, 15);
        decimal drawdownScore = NormalizeScore(maxDrawdown, -65, -10);
        decimal consistencyScore = NormalizeScore(posDayPct, 20, 50);
        
        decimal trendScoreVal = trendClass switch
        {
            "STRONGER_UPTREND" => 100,
            "UPTREND" => 75,
            "WEAKENING_OR_MIXED" => 50,
            "DOWNTREND" => 25,
            _ => 0
        };
        
        decimal overallScore = (returnScore * 0.30m) + (riskScore * 0.20m) + (drawdownScore * 0.20m) + (consistencyScore * 0.15m) + (trendScoreVal * 0.15m);
        
        string scoreClass = overallScore switch
        {
            >= 75 => "Strong Historical Profile",
            >= 60 => "Favorable Historical Profile",
            >= 45 => "Moderate Historical Profile",
            >= 30 => "Higher Historical Risk",
            _ => "High Historical Risk"
        };
        
        profile.Score = new ScoreDto
        {
            OverallScore = Math.Round(overallScore, 2),
            ReturnScore = Math.Round(returnScore, 2),
            RiskScore = Math.Round(riskScore, 2),
            DrawdownScore = Math.Round(drawdownScore, 2),
            ConsistencyScore = Math.Round(consistencyScore, 2),
            TrendScore = Math.Round(trendScoreVal, 2),
            Classification = scoreClass
        };
        
        // Explanations
        if (cagr > 0) profile.Explanations.PositiveFactors.Add("Historical CAGR was positive over the analysis period.");
        if (historicalReturn > 0) profile.Explanations.PositiveFactors.Add("Historical price increased overall across the observation window.");
        if (posDayPct > 35) profile.Explanations.PositiveFactors.Add("Positive price changes occurred consistently across trading sessions.");
        if (latestMa50.HasValue && endingPrice > latestMa50.Value) profile.Explanations.PositiveFactors.Add("Latest closing price is above the 50-day moving average.");
        if (latestMa200.HasValue && endingPrice > latestMa200.Value) profile.Explanations.PositiveFactors.Add("Latest closing price is above the 200-day moving average.");
        
        if (annVolatility > 55) profile.Explanations.RiskFactors.Add("Historical annualized volatility was relatively elevated.");
        if (maxDrawdown < -45) profile.Explanations.RiskFactors.Add("The company experienced a noticeable historical peak-to-trough drawdown.");
        if (!hasFullFiveYearHistory) profile.Explanations.RiskFactors.Add("Historical data coverage is limited to under five full calendar years.");
        
        if (trendClass == "STRONGER_UPTREND") profile.Explanations.TrendFactors.Add("Latest price is above both the 50-day and 200-day moving averages.");
        if (latestMa50.HasValue && latestMa200.HasValue && latestMa50.Value < latestMa200.Value) profile.Explanations.TrendFactors.Add("50-day moving average is below the 200-day moving average.");
        
        // Data Quality
        profile.DataQuality = new DataQualityDto
        {
            HasFullFiveYearHistory = hasFullFiveYearHistory,
            AnalysisObservationCount = windowData.Count,
            FirstValidDate = startDate.ToString("yyyy-MM-dd"),
            LastValidDate = endDate.ToString("yyyy-MM-dd")
        };
        
        if (!hasFullFiveYearHistory) profile.DataQuality.DataQualityWarnings.Add("Less than five years of valid history.");
        if (!latestMa200.HasValue) profile.DataQuality.DataQualityWarnings.Add("Moving averages could not be calculated because insufficient observations were available.");

        return ApiResponse<HistoricalInvestmentProfileDto>.Ok(profile);
    }
    
    private decimal NormalizeScore(decimal value, decimal minRange, decimal maxRange)
    {
        if (maxRange == minRange) return 50;
        
        decimal score = ((value - minRange) / (maxRange - minRange)) * 100;
        if (score > 100) return 100;
        if (score < 0) return 0;
        return score;
    }
    
    private ApiResponse<HistoricalInvestmentProfileDto> CreateEmptyProfile(Company company)
    {
        var profile = new HistoricalInvestmentProfileDto
        {
            Company = new CompanyInfoDto
            {
                Id = company.CompanyId,
                Name = company.CompanyName,
                TradingCode = company.TickerSymbol,
                Sector = company.Sector?.SectorName ?? "Unknown"
            },
            DataQuality = new DataQualityDto
            {
                HasFullFiveYearHistory = false,
                AnalysisObservationCount = 0
            }
        };
        profile.DataQuality.DataQualityWarnings.Add("Company has no valid historical data.");
        return ApiResponse<HistoricalInvestmentProfileDto>.Ok(profile, "Company has no valid historical data.");
    }

    private async Task SeedFromCsvIfAvailableAsync(int companyId, string tickerSymbol, CancellationToken cancellationToken)
    {
        try
        {
            var csvPath = @"E:\Projects\Oracle+WebProgramming\ShareSync\Dhaka Stock Exchange Historical Data (1999-2025)\DSE_Data.csv";
            if (!System.IO.File.Exists(csvPath)) return;

            var normalizedTicker = tickerSymbol.Trim().ToUpperInvariant();
            var lines = new List<string>();

            using (var reader = new System.IO.StreamReader(csvPath))
            {
                string? header = await reader.ReadLineAsync(cancellationToken);
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(',');
                    if (parts.Length >= 7 && string.Equals(parts[0].Trim(), normalizedTicker, StringComparison.OrdinalIgnoreCase))
                    {
                        lines.Add(line);
                    }
                }
            }

            if (!lines.Any()) return;

            var dedup = new Dictionary<string, CompanyPriceHistory>();
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                var dateStr = parts[1].Trim();
                var dParts = dateStr.Split('-');
                if (dParts.Length != 3) continue;

                if (!int.TryParse(dParts[0], out int y) || !int.TryParse(dParts[1], out int m) || !int.TryParse(dParts[2], out int d))
                    continue;

                DateTime validDate = new DateTime(y, m, d);

                if (!decimal.TryParse(parts[5].Trim(), out decimal close) || close <= 0) continue;
                if (!decimal.TryParse(parts[2].Trim(), out decimal open) || open <= 0) open = close;
                if (!decimal.TryParse(parts[3].Trim(), out decimal high) || high <= 0) high = Math.Max(open, close);
                if (!decimal.TryParse(parts[4].Trim(), out decimal low) || low <= 0) low = Math.Min(open, close);
                long? vol = long.TryParse(parts[6].Trim(), out long v) ? v : null;

                var key = validDate.ToString("yyyy-MM-dd");
                dedup[key] = new CompanyPriceHistory
                {
                    CompanyId = companyId,
                    TradingDate = validDate,
                    RecordedAt = validDate,
                    Price = close,
                    OpenPrice = open,
                    HighPrice = high,
                    LowPrice = low,
                    Volume = vol,
                    Source = "Harvard Dataverse",
                    SourceDataset = "Dhaka Stock Exchange Historical Data",
                    SourceDoi = "10.7910/DVN/XIFYT1",
                    CreatedAt = DateTime.UtcNow
                };
            }

            if (dedup.Any())
            {
                await _context.CompanyPriceHistories.AddRangeAsync(dedup.Values, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        catch
        {
            // Silently catch to not break primary flow
        }
    }
}
