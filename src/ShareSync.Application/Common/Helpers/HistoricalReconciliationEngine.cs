using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShareSync.Application.Common.Helpers;

/// <summary>
/// Status of a historical record during staging and validation.
/// </summary>
public enum HistoricalValidationStatus
{
    Pending,
    Valid,
    ExactDuplicate,
    Conflicting,
    InvalidPrice,
    InvalidOhlc,
    InvalidDate,
    Unmapped
}

/// <summary>
/// Normalized raw historical observation parsed from source files.
/// </summary>
public class RawHistoricalObservation
{
    public string RawTicker { get; set; } = string.Empty;
    public string RawDate { get; set; } = string.Empty;
    public string? RawOpen { get; set; }
    public string? RawHigh { get; set; }
    public string? RawLow { get; set; }
    public string RawClose { get; set; } = string.Empty;
    public string? RawVolume { get; set; }
    public string Source { get; set; } = "Harvard Dataverse";
    public string SourceDataset { get; set; } = "Dhaka Stock Exchange Historical Data (1999-2025)";
    public string SourceDoi { get; set; } = "10.7910/DVN/XIFYT1";
    public string ImportBatchId { get; set; } = string.Empty;
}

/// <summary>
/// Staged and reconciled historical observation.
/// </summary>
public class StagedHistoricalRecord
{
    public string TradingCode { get; set; } = string.Empty;
    public int? CompanyId { get; set; }
    public DateTime TradingDate { get; set; }
    public decimal? OpenPrice { get; set; }
    public decimal? HighPrice { get; set; }
    public decimal? LowPrice { get; set; }
    public decimal ClosePrice { get; set; }
    public long? Volume { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? SourceDataset { get; set; }
    public string? SourceDoi { get; set; }
    public string ImportBatchId { get; set; } = string.Empty;
    public HistoricalValidationStatus Status { get; set; } = HistoricalValidationStatus.Pending;
    public string? ValidationNotes { get; set; }
}

/// <summary>
/// Coverage statistics for an equity or benchmark time series.
/// </summary>
public class HistoricalCoverageStat
{
    public string InstrumentCode { get; set; } = string.Empty;
    public DateTime? EarliestDate { get; set; }
    public DateTime? LatestDate { get; set; }
    public int TotalObservations { get; set; }
    public double CoverageYears { get; set; }
    public bool HasFiveYears { get; set; }
    public bool HasThreeYears { get; set; }
    public bool HasLessThanOneYear { get; set; }
    public int GapsOverSevenDays { get; set; }
    public int MaxGapDays { get; set; }
    public bool HasRecentData { get; set; }
}

/// <summary>
/// Core domain reconciliation engine implementing Phase 1-6 business rules.
/// </summary>
public static class HistoricalReconciliationEngine
{
    /// <summary>
    /// Parses and validates a raw company observation into a staged record.
    /// </summary>
    public static StagedHistoricalRecord StageCompanyObservation(
        RawHistoricalObservation raw,
        IDictionary<string, int> tickerToCompanyIdMap)
    {
        var record = new StagedHistoricalRecord
        {
            TradingCode = raw.RawTicker.Trim(),
            Source = raw.Source,
            SourceDataset = raw.SourceDataset,
            SourceDoi = raw.SourceDoi,
            ImportBatchId = raw.ImportBatchId
        };

        // 1. Validate Date
        if (!MarketDataPathResolver.TryParseTradingDate(raw.RawDate, out DateTime tradingDate))
        {
            record.Status = HistoricalValidationStatus.InvalidDate;
            record.ValidationNotes = $"Failed to parse trading date '{raw.RawDate}'.";
            return record;
        }
        record.TradingDate = tradingDate.Date;

        // 2. Resolve Company ID via canonical ticker normalization
        var canonicalTicker = MarketDataPathResolver.ResolveCanonicalTicker(raw.RawTicker);
        if (tickerToCompanyIdMap.TryGetValue(canonicalTicker, out int companyId) ||
            tickerToCompanyIdMap.TryGetValue(MarketDataPathResolver.NormalizeTicker(raw.RawTicker), out companyId))
        {
            record.CompanyId = companyId;
        }
        else
        {
            record.Status = HistoricalValidationStatus.Unmapped;
            record.ValidationNotes = $"Unmapped trading code '{raw.RawTicker}' (canonical: '{canonicalTicker}').";
            return record;
        }

        // 3. Validate Close Price
        if (!decimal.TryParse(raw.RawClose.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal close) || close <= 0)
        {
            record.Status = HistoricalValidationStatus.InvalidPrice;
            record.ValidationNotes = $"Invalid non-positive close price '{raw.RawClose}'.";
            return record;
        }
        record.ClosePrice = close;

        // 4. Parse OHLC components
        decimal? open = null;
        if (!string.IsNullOrWhiteSpace(raw.RawOpen) &&
            decimal.TryParse(raw.RawOpen.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal o))
        {
            open = o;
        }

        decimal? high = null;
        if (!string.IsNullOrWhiteSpace(raw.RawHigh) &&
            decimal.TryParse(raw.RawHigh.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal h))
        {
            high = h;
        }

        decimal? low = null;
        if (!string.IsNullOrWhiteSpace(raw.RawLow) &&
            decimal.TryParse(raw.RawLow.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal l))
        {
            low = l;
        }

        // 5. Strict OHLC Validation
        if (!MarketDataPathResolver.StrictValidateOhlc(close, open, high, low, out string? ohlcError))
        {
            record.Status = HistoricalValidationStatus.InvalidOhlc;
            record.ValidationNotes = ohlcError;
            record.OpenPrice = open;
            record.HighPrice = high;
            record.LowPrice = low;
            return record;
        }

        record.OpenPrice = open;
        record.HighPrice = high;
        record.LowPrice = low;

        // 6. Parse Volume (0 is valid; missing is NULL)
        if (!string.IsNullOrWhiteSpace(raw.RawVolume))
        {
            var cleanVol = raw.RawVolume.Trim();
            if (long.TryParse(cleanVol, NumberStyles.Any, CultureInfo.InvariantCulture, out long vol) && vol >= 0)
            {
                record.Volume = vol;
            }
            else if (decimal.TryParse(cleanVol, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal volDec) && volDec >= 0)
            {
                record.Volume = (long)volDec;
            }
        }

        record.Status = HistoricalValidationStatus.Valid;
        return record;
    }

    /// <summary>
    /// Parses and validates a raw benchmark observation into a staged record.
    /// </summary>
    public static StagedHistoricalRecord StageBenchmarkObservation(RawHistoricalObservation raw)
    {
        var record = new StagedHistoricalRecord
        {
            TradingCode = raw.RawTicker.Trim().ToUpperInvariant(),
            Source = raw.Source,
            SourceDataset = raw.SourceDataset,
            SourceDoi = raw.SourceDoi,
            ImportBatchId = raw.ImportBatchId
        };

        if (!MarketDataPathResolver.TryParseTradingDate(raw.RawDate, out DateTime tradingDate))
        {
            record.Status = HistoricalValidationStatus.InvalidDate;
            record.ValidationNotes = $"Failed to parse benchmark date '{raw.RawDate}'.";
            return record;
        }
        record.TradingDate = tradingDate.Date;

        if (!decimal.TryParse(raw.RawClose.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal close) || close <= 0)
        {
            record.Status = HistoricalValidationStatus.InvalidPrice;
            record.ValidationNotes = $"Invalid non-positive benchmark value '{raw.RawClose}'.";
            return record;
        }
        record.ClosePrice = close;

        decimal? open = null;
        if (!string.IsNullOrWhiteSpace(raw.RawOpen) &&
            decimal.TryParse(raw.RawOpen.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal o))
            open = o;

        decimal? high = null;
        if (!string.IsNullOrWhiteSpace(raw.RawHigh) &&
            decimal.TryParse(raw.RawHigh.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal h))
            high = h;

        decimal? low = null;
        if (!string.IsNullOrWhiteSpace(raw.RawLow) &&
            decimal.TryParse(raw.RawLow.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal l))
            low = l;

        if (!MarketDataPathResolver.StrictValidateOhlc(close, open, high, low, out string? ohlcError))
        {
            record.Status = HistoricalValidationStatus.InvalidOhlc;
            record.ValidationNotes = ohlcError;
            record.OpenPrice = open;
            record.HighPrice = high;
            record.LowPrice = low;
            return record;
        }

        record.OpenPrice = open;
        record.HighPrice = high;
        record.LowPrice = low;

        if (!string.IsNullOrWhiteSpace(raw.RawVolume))
        {
            var cleanVol = raw.RawVolume.Trim();
            if (long.TryParse(cleanVol, NumberStyles.Any, CultureInfo.InvariantCulture, out long vol) && vol >= 0)
                record.Volume = vol;
            else if (decimal.TryParse(cleanVol, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal volDec) && volDec >= 0)
                record.Volume = (long)volDec;
        }

        record.Status = HistoricalValidationStatus.Valid;
        return record;
    }

    /// <summary>
    /// Reconciles duplicates across staged records according to Phase 4 rules:
    /// - Category A (Exact duplicates): collapsed into 1 valid record.
    /// - Category B (Same entity/date, identical OHLC, differing metadata): collapsed into 1 valid record retaining primary provenance.
    /// - Category C (Conflicting OHLC): marked CONFLICTING and excluded from canonical promotion.
    /// </summary>
    public static (List<StagedHistoricalRecord> Promotable, List<StagedHistoricalRecord> Conflicting, int ExactDuplicateCount) ReconcileDuplicates(
        IEnumerable<StagedHistoricalRecord> records,
        Func<StagedHistoricalRecord, string> keySelector)
    {
        var promotable = new List<StagedHistoricalRecord>();
        var conflicting = new List<StagedHistoricalRecord>();
        int exactDuplicateCount = 0;

        var grouped = records
            .Where(r => r.Status == HistoricalValidationStatus.Valid)
            .GroupBy(keySelector);

        foreach (var group in grouped)
        {
            var list = group.ToList();
            if (list.Count == 1)
            {
                promotable.Add(list[0]);
                continue;
            }

            // Check if all items have identical OHLC
            var first = list[0];
            bool hasIdenticalOhlc = list.All(r =>
                r.ClosePrice == first.ClosePrice &&
                r.OpenPrice == first.OpenPrice &&
                r.HighPrice == first.HighPrice &&
                r.LowPrice == first.LowPrice);

            if (hasIdenticalOhlc)
            {
                // Category A or B: collapse safely into one canonical record
                exactDuplicateCount += (list.Count - 1);
                promotable.Add(first);
            }
            else
            {
                // Category C: Conflicting OHLC! DO NOT guess or silently overwrite.
                foreach (var conf in list)
                {
                    conf.Status = HistoricalValidationStatus.Conflicting;
                    conf.ValidationNotes = $"Conflicting OHLC observation detected for key '{group.Key}'.";
                    conflicting.Add(conf);
                }
            }
        }

        return (promotable, conflicting, exactDuplicateCount);
    }

    /// <summary>
    /// Computes accurate coverage statistics for a series of distinct trading dates.
    /// Distinguishes weekends and holidays (no trading) from true historical gaps (> 7 calendar days).
    /// </summary>
    public static HistoricalCoverageStat CalculateCoverage(string instrumentCode, IEnumerable<DateTime> tradingDates)
    {
        var dates = tradingDates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
        if (!dates.Any())
        {
            return new HistoricalCoverageStat
            {
                InstrumentCode = instrumentCode,
                TotalObservations = 0,
                HasLessThanOneYear = true
            };
        }

        var earliest = dates.First();
        var latest = dates.Last();
        double years = (latest - earliest).TotalDays / 365.25;

        int gapsOver7 = 0;
        int maxGap = 0;

        for (int i = 0; i < dates.Count - 1; i++)
        {
            int diff = (int)(dates[i + 1] - dates[i]).TotalDays;
            if (diff > maxGap) maxGap = diff;
            if (diff > 7) gapsOver7++;
        }

        return new HistoricalCoverageStat
        {
            InstrumentCode = instrumentCode,
            EarliestDate = earliest,
            LatestDate = latest,
            TotalObservations = dates.Count,
            CoverageYears = Math.Round(years, 2),
            HasFiveYears = years >= 4.7,
            HasThreeYears = years >= 2.8,
            HasLessThanOneYear = years < 1.0,
            GapsOverSevenDays = gapsOver7,
            MaxGapDays = maxGap,
            HasRecentData = latest.Year >= 2024
        };
    }
}
