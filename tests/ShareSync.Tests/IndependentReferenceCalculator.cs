using System;
using System.Collections.Generic;
using System.Linq;

namespace ShareSync.Tests;

/// <summary>
/// Phase 2: Independent reference calculation engine built with explicit arithmetic
/// and zero dependency on ShareSync production services or domain methods.
/// Used to cross-verify production outputs against independent reference truth.
/// </summary>
public static class IndependentReferenceCalculator
{
    public record HoldingsReferenceResult(
        decimal RemainingQuantity,
        decimal WeightedAverageBuyPrice,
        decimal InvestedValue,
        decimal MarketValue,
        decimal UnrealizedProfitLoss,
        decimal UnrealizedProfitLossPercentage,
        decimal RealizedProfitLoss,
        decimal TotalFees,
        decimal NetCashFlow
    );

    /// <summary>
    /// Independently computes portfolio holdings, cost basis, realized P/L, fees, and cash flows.
    /// </summary>
    public static HoldingsReferenceResult EvaluateHoldings(
        IEnumerable<(string Type, decimal Qty, decimal Price, decimal Fee)> transactions,
        decimal currentMarketPrice)
    {
        var txList = transactions.ToList();

        decimal totalBoughtQty = 0m;
        decimal totalBoughtCost = 0m;
        decimal totalSoldQty = 0m;
        decimal totalRealizedPL = 0m;
        decimal totalFees = 0m;
        decimal totalCashOutflow = 0m;
        decimal totalCashInflow = 0m;

        // Cumulative buy tracking for weighted average
        foreach (var tx in txList)
        {
            totalFees += tx.Fee;
            var gross = tx.Qty * tx.Price;

            if (tx.Type.Equals("BUY", StringComparison.OrdinalIgnoreCase))
            {
                totalBoughtQty += tx.Qty;
                totalBoughtCost += gross;
                totalCashOutflow += (gross + tx.Fee);
            }
        }

        decimal weightedAvgCost = totalBoughtQty > 0 ? totalBoughtCost / totalBoughtQty : 0m;

        // Process sells
        foreach (var tx in txList)
        {
            if (tx.Type.Equals("SELL", StringComparison.OrdinalIgnoreCase))
            {
                totalSoldQty += tx.Qty;
                var gross = tx.Qty * tx.Price;
                totalCashInflow += (gross - tx.Fee);

                // Realized P/L: Net Sale Proceeds - Cost Basis Sold
                var netProceeds = gross - tx.Fee;
                var costBasisSold = tx.Qty * weightedAvgCost;
                totalRealizedPL += (netProceeds - costBasisSold);
            }
        }

        decimal remainingQty = totalBoughtQty - totalSoldQty;
        decimal investedValue = remainingQty * weightedAvgCost;
        decimal marketValue = remainingQty * currentMarketPrice;
        decimal unrealizedPL = marketValue - investedValue;
        decimal unrealizedPLPct = investedValue > 0 ? (unrealizedPL / investedValue) * 100m : 0m;
        decimal netCashFlow = totalCashInflow - totalCashOutflow;

        return new HoldingsReferenceResult(
            RemainingQuantity: remainingQty,
            WeightedAverageBuyPrice: Math.Round(weightedAvgCost, 2),
            InvestedValue: Math.Round(investedValue, 2),
            MarketValue: Math.Round(marketValue, 2),
            UnrealizedProfitLoss: Math.Round(unrealizedPL, 2),
            UnrealizedProfitLossPercentage: Math.Round(unrealizedPLPct, 2),
            RealizedProfitLoss: Math.Round(totalRealizedPL, 2),
            TotalFees: Math.Round(totalFees, 2),
            NetCashFlow: Math.Round(netCashFlow, 2)
        );
    }

    /// <summary>
    /// Independently computes historical percentage return over an explicit baseline.
    /// </summary>
    public static decimal CalculateSimpleReturn(decimal startPrice, decimal endPrice)
    {
        if (startPrice <= 0m) return 0m;
        return Math.Round(((endPrice - startPrice) / startPrice) * 100m, 2);
    }

    /// <summary>
    /// Independently computes Compound Annual Growth Rate (CAGR).
    /// </summary>
    public static decimal CalculateCagr(decimal startPrice, decimal endPrice, double years)
    {
        if (years <= 0 || startPrice <= 0m || endPrice <= 0m) return 0m;
        double ratio = (double)(endPrice / startPrice);
        double cagr = (Math.Pow(ratio, 1.0 / years) - 1.0) * 100.0;
        return Math.Round((decimal)cagr, 2);
    }

    /// <summary>
    /// Independently computes sequential daily returns (index 0 produces no return).
    /// </summary>
    public static List<decimal> CalculateDailyReturns(IList<decimal> chronologicalPrices)
    {
        var returns = new List<decimal>();
        for (int i = 1; i < chronologicalPrices.Count; i++)
        {
            var prev = chronologicalPrices[i - 1];
            var curr = chronologicalPrices[i];
            if (prev > 0m)
            {
                var r = ((curr / prev) - 1m) * 100m;
                returns.Add(r);
            }
        }
        return returns;
    }

    /// <summary>
    /// Independently computes sample standard deviation scaled by sqrt(252).
    /// </summary>
    public static decimal CalculateAnnualizedVolatility(IList<decimal> dailyReturns)
    {
        if (dailyReturns.Count < 2) return 0m;

        decimal mean = dailyReturns.Average();
        decimal sumSq = dailyReturns.Sum(r => (r - mean) * (r - mean));
        double sampleVariance = (double)(sumSq / (dailyReturns.Count - 1));
        double dailyVol = Math.Sqrt(sampleVariance);
        double annVol = dailyVol * Math.Sqrt(252);
        return Math.Round((decimal)annVol, 2);
    }

    /// <summary>
    /// Independently computes sequential running peak-to-trough maximum drawdown.
    /// </summary>
    public static decimal CalculateMaximumDrawdown(IList<decimal> chronologicalPrices)
    {
        if (chronologicalPrices.Count == 0) return 0m;

        decimal runningPeak = chronologicalPrices[0];
        decimal maxDrawdown = 0m;

        foreach (var price in chronologicalPrices)
        {
            if (price > runningPeak)
            {
                runningPeak = price;
            }

            decimal dd = runningPeak > 0m ? ((price - runningPeak) / runningPeak) * 100m : 0m;
            if (dd < maxDrawdown)
            {
                maxDrawdown = dd;
            }
        }

        return Math.Round(maxDrawdown, 2);
    }

    /// <summary>
    /// Independently computes Simple Moving Average for a specified window size.
    /// Returns null if fewer than 'window' observations exist.
    /// </summary>
    public static decimal? CalculateSma(IList<decimal> chronologicalPrices, int window)
    {
        if (chronologicalPrices.Count < window || window <= 0)
        {
            return null;
        }

        decimal sum = chronologicalPrices.TakeLast(window).Sum();
        return Math.Round(sum / window, 2);
    }

    /// <summary>
    /// Independently computes Fisher real return: ((1 + Nom) / (1 + Inf) - 1) * 100.
    /// </summary>
    public static decimal? CalculateRealReturn(decimal nominalReturnPct, decimal? inflationRatePct)
    {
        if (!inflationRatePct.HasValue) return null;

        decimal nomFactor = 1.0m + (nominalReturnPct / 100.0m);
        decimal infFactor = 1.0m + (inflationRatePct.Value / 100.0m);
        if (infFactor == 0m) return 0m;

        decimal realFactor = (nomFactor / infFactor) - 1.0m;
        return Math.Round(realFactor * 100.0m, 2);
    }

    /// <summary>
    /// Independently computes multi-year cumulative compounding inflation factor.
    /// </summary>
    public static decimal CalculateCumulativeInflation(IEnumerable<decimal> annualInflationPercentages)
    {
        decimal factor = 1.0m;
        foreach (var rate in annualInflationPercentages)
        {
            factor *= (1.0m + (rate / 100.0m));
        }
        return Math.Round((factor - 1.0m) * 100.0m, 2);
    }

    /// <summary>
    /// Independently computes the Investment Intelligence score model.
    /// </summary>
    public static (decimal Score, string Classification) EvaluateInvestmentScore(
        decimal cagr,
        decimal annVol,
        decimal maxDrawdown,
        decimal posDayPct,
        string trendClass)
    {
        decimal retScore = Clamp((cagr - (-10m)) / (20m - (-10m)) * 100m, 0m, 100m);
        decimal riskScore = Clamp((annVol - 75m) / (15m - 75m) * 100m, 0m, 100m);
        decimal ddScore = Clamp((maxDrawdown - (-65m)) / (-10m - (-65m)) * 100m, 0m, 100m);
        decimal conScore = Clamp((posDayPct - 20m) / (50m - 20m) * 100m, 0m, 100m);

        decimal trendScore = trendClass switch
        {
            "STRONGER_UPTREND" => 100m,
            "UPTREND" => 75m,
            "WEAKENING_OR_MIXED" => 50m,
            "DOWNTREND" => 25m,
            _ => 0m
        };

        decimal overall = (retScore * 0.30m) + (riskScore * 0.20m) + (ddScore * 0.20m) + (conScore * 0.15m) + (trendScore * 0.15m);
        overall = Math.Round(overall, 2);

        string classification = overall switch
        {
            >= 75m => "Strong Historical Profile",
            >= 60m => "Favorable Historical Profile",
            >= 45m => "Moderate Historical Profile",
            >= 30m => "Higher Historical Risk",
            _ => "High Historical Risk"
        };

        return (overall, classification);
    }

    private static decimal Clamp(decimal val, decimal min, decimal max)
    {
        if (val < min) return min;
        if (val > max) return max;
        return val;
    }
}
