using System;
using System.Globalization;
using System.IO;

namespace ShareSync.Application.Common.Helpers;

/// <summary>
/// Authoritative helper for dataset path resolution, ticker canonicalization, and OHLC validation.
/// Ensures resilient historical data retrieval across development, test, and production environments.
/// </summary>
public static class MarketDataPathResolver
{
    private static readonly string DatasetFolderName = "Dhaka Stock Exchange Historical Data (1999-2025)";
    private static readonly string MainCsvFileName = "DSE_Data.csv";

    /// <summary>
    /// Dynamically locates the canonical DSE_Data.csv file across standard execution probe paths.
    /// </summary>
    public static string? LocateDseCsv()
    {
        var probePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), DatasetFolderName, MainCsvFileName),
            Path.Combine(Directory.GetCurrentDirectory(), MainCsvFileName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DatasetFolderName, MainCsvFileName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", DatasetFolderName, MainCsvFileName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", DatasetFolderName, MainCsvFileName),
            Path.Combine(@"E:\Projects\Oracle+WebProgramming\ShareSync_MySQL", DatasetFolderName, MainCsvFileName),
            Path.Combine(@"E:\Projects\Oracle+WebProgramming\ShareSync", DatasetFolderName, MainCsvFileName)
        };

        foreach (var path in probePaths)
        {
            try
            {
                var full = Path.GetFullPath(path);
                if (File.Exists(full))
                {
                    return full;
                }
            }
            catch
            {
                // Ignore invalid path syntax during probing
            }
        }

        return null;
    }

    /// <summary>
    /// Normalizes a ticker symbol to uppercase invariant and trims whitespace.
    /// Preserves valid hyphenated cash instruments (e.g. CASH-BDT, CASH-USD).
    /// </summary>
    public static string NormalizeTicker(string? tickerSymbol)
    {
        if (string.IsNullOrWhiteSpace(tickerSymbol))
        {
            return string.Empty;
        }

        return tickerSymbol.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Parses an ISO 8601 date (yyyy-MM-dd) without ambiguous day/month swapping.
    /// </summary>
    public static bool TryParseTradingDate(string rawDate, out DateTime tradingDate)
    {
        tradingDate = default;
        if (string.IsNullOrWhiteSpace(rawDate))
        {
            return false;
        }

        var trimmed = rawDate.Trim();

        // 1. Try exact ISO-8601 date
        if (DateTime.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out tradingDate))
        {
            return true;
        }

        // 2. Try general invariant date
        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out tradingDate))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Validates price plausibility and OHLC sanity.
    /// </summary>
    public static bool IsValidPrice(decimal price)
    {
        return price > 0 && price <= 500000m;
    }

    /// <summary>
    /// Maps a raw ticker or legacy alias to the canonical tracked company symbol.
    /// Handles whitespace, casing, and punctuation differences.
    /// </summary>
    public static string ResolveCanonicalTicker(string? rawTicker)
    {
        if (string.IsNullOrWhiteSpace(rawTicker))
            return string.Empty;

        var normalized = NormalizeTicker(rawTicker);

        // Map known DSE abbreviations and legacy aliases to canonical database symbols
        return normalized switch
        {
            "SQURPHARMA" or "SQUARE" or "SQUR" => "SQUAREPHARMA",
            "GRAMEENS2" or "GRAMEENONE" or "GRAMEEN1" => "GRAMEEN1MF",
            "AMCLPRAN" or "AMCL-PRAN" or "PRAN" => "AMCL(PRAN)",
            "BEXIMCOPHARMA" => "BXPHARMA",
            "LAFARGE" or "LAFARGEHOLCIM" => "LHBL",
            "WALTON" => "WALTONHIL",
            "BRAC" => "BRACBANK",
            _ => normalized
        };
    }

    /// <summary>
    /// Gets candidate CSV filenames for an instrument or benchmark symbol.
    /// </summary>
    public static string[] GetCandidateFilenames(string symbol)
    {
        var norm = NormalizeTicker(symbol);
        var canonical = ResolveCanonicalTicker(norm);

        var list = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"{norm}.csv",
            $"{canonical}.csv"
        };

        if (canonical == "SQUAREPHARMA") list.Add("SQURPHARMA.csv");
        if (canonical == "GRAMEEN1MF") list.Add("GRAMEENS2.csv");
        if (canonical == "AMCL(PRAN)") { list.Add("AMCLPRAN.csv"); list.Add("AMCL(PRAN).csv"); }
        if (canonical == "LHBL") list.Add("LHBL.csv");
        if (canonical == "BXPHARMA") list.Add("BXPHARMA.csv");
        if (canonical == "BRACBANK") list.Add("BRACBANK.csv");
        if (canonical == "BATBC") list.Add("BATBC.csv");
        if (canonical == "GP") list.Add("GP.csv");
        if (canonical == "RENATA") list.Add("RENATA.csv");
        if (canonical == "WALTONHIL") list.Add("WALTONHIL.csv");
        if (canonical == "MARICO") list.Add("MARICO.csv");

        // Indices
        if (norm == "DSEX") list.Add("DSEX.csv");
        if (norm == "DS30") list.Add("DS30.csv");
        if (norm == "DSES") list.Add("DSES.csv");

        return list.ToArray();
    }

    /// <summary>
    /// Locates an instrument or benchmark CSV file across known dataset probe locations.
    /// </summary>
    public static string? LocateInstrumentCsv(string symbol)
    {
        var candidates = GetCandidateFilenames(symbol);
        var baseDirs = new[]
        {
            Directory.GetCurrentDirectory(),
            Path.Combine(Directory.GetCurrentDirectory(), DatasetFolderName),
            Path.Combine(Directory.GetCurrentDirectory(), DatasetFolderName, "Instrument_Based"),
            AppDomain.CurrentDomain.BaseDirectory,
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DatasetFolderName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DatasetFolderName, "Instrument_Based"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", DatasetFolderName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", DatasetFolderName, "Instrument_Based"),
            Path.Combine(@"E:\Projects\Oracle+WebProgramming\ShareSync_MySQL", DatasetFolderName),
            Path.Combine(@"E:\Projects\Oracle+WebProgramming\ShareSync_MySQL", DatasetFolderName, "Instrument_Based")
        };

        foreach (var dir in baseDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var cand in candidates)
            {
                var full = Path.GetFullPath(Path.Combine(dir, cand));
                if (File.Exists(full)) return full;
            }
        }

        return null;
    }

    /// <summary>
    /// Performs strict Phase 1/Phase 2 OHLC validation without modifying values.
    /// Verifies Low <= Open <= High and Low <= Close <= High when present.
    /// Rejects non-positive prices or impossible bounds.
    /// </summary>
    public static bool StrictValidateOhlc(
        decimal close,
        decimal? open,
        decimal? high,
        decimal? low,
        out string? failureReason)
    {
        if (close <= 0)
        {
            failureReason = "Close price must be strictly positive.";
            return false;
        }

        if (open.HasValue && open.Value <= 0)
        {
            failureReason = "Open price must be strictly positive when specified.";
            return false;
        }

        if (high.HasValue && high.Value <= 0)
        {
            failureReason = "High price must be strictly positive when specified.";
            return false;
        }

        if (low.HasValue && low.Value <= 0)
        {
            failureReason = "Low price must be strictly positive when specified.";
            return false;
        }

        if (high.HasValue && low.HasValue && high.Value < low.Value)
        {
            failureReason = $"High ({high.Value}) cannot be strictly lower than Low ({low.Value}).";
            return false;
        }

        if (open.HasValue && high.HasValue && open.Value > high.Value)
        {
            failureReason = $"Open ({open.Value}) cannot exceed High ({high.Value}).";
            return false;
        }

        if (open.HasValue && low.HasValue && open.Value < low.Value)
        {
            failureReason = $"Open ({open.Value}) cannot be below Low ({low.Value}).";
            return false;
        }

        if (high.HasValue && close > high.Value)
        {
            failureReason = $"Close ({close}) cannot exceed High ({high.Value}).";
            return false;
        }

        if (low.HasValue && close < low.Value)
        {
            failureReason = $"Close ({close}) cannot be below Low ({low.Value}).";
            return false;
        }

        failureReason = null;
        return true;
    }

    /// <summary>
    /// Validates and harmonizes OHLC bounds: High >= Low, High >= Close, Low <= Close.
    /// </summary>
    public static bool ValidateAndHarmonizeOhlc(
        decimal close,
        ref decimal? open,
        ref decimal? high,
        ref decimal? low)
    {
        if (!IsValidPrice(close))
        {
            return false;
        }

        if (open.HasValue && (!IsValidPrice(open.Value) || open.Value <= 0))
        {
            open = close;
        }

        if (high.HasValue && (!IsValidPrice(high.Value) || high.Value <= 0))
        {
            high = Math.Max(open ?? close, close);
        }

        if (low.HasValue && (!IsValidPrice(low.Value) || low.Value <= 0))
        {
            low = Math.Min(open ?? close, close);
        }

        if (high.HasValue && low.HasValue && high.Value < low.Value)
        {
            decimal temp = high.Value;
            high = low.Value;
            low = temp;
        }

        if (high.HasValue && high.Value < close)
        {
            high = close;
        }

        if (low.HasValue && low.Value > close)
        {
            low = close;
        }

        return true;
    }

    /// <summary>
    /// Validates quote timestamp plausibility.
    /// Flags future timestamps as invalid.
    /// Flags timestamps older than 7 calendar days as STALE.
    /// </summary>
    public static bool ValidateQuoteTimestamp(
        DateTime? timestamp,
        DateTime referenceTime,
        out string status,
        out string? validationError)
    {
        if (!timestamp.HasValue)
        {
            status = "LIVE";
            validationError = null;
            return true;
        }

        // Allow up to 15 minutes of clock skew into the future
        if (timestamp.Value > referenceTime.AddMinutes(15))
        {
            status = "FUTURE_REJECTED";
            validationError = $"Quote timestamp {timestamp.Value:yyyy-MM-dd HH:mm:ss} is in the future relative to server time {referenceTime:yyyy-MM-dd HH:mm:ss}.";
            return false;
        }

        // Quotes older than 7 days are considered stale (non-trading period or unrefreshed feed)
        if (timestamp.Value < referenceTime.AddDays(-7))
        {
            status = "STALE";
            validationError = null;
            return true;
        }

        status = "LIVE";
        validationError = null;
        return true;
    }

    /// <summary>
    /// Reconciles an incoming price quote against the current stored price.
    /// Distinguishes MATCH, ACCEPTABLE_DIFFERENCE, SIGNIFICANT_DIFFERENCE, and EXTREME_ANOMALY.
    /// Extreme anomalies (ratio > 10.0x or < 0.05x) are rejected to protect portfolio valuation.
    /// </summary>
    public static bool ReconcilePriceMovement(
        decimal currentPrice,
        decimal newPrice,
        out string reconciliationResult,
        out string? message)
    {
        if (!IsValidPrice(newPrice))
        {
            reconciliationResult = "INVALID_PRICE";
            message = $"Incoming price {newPrice} is invalid (must be positive and <= 500,000).";
            return false;
        }

        if (currentPrice <= 0)
        {
            reconciliationResult = "INITIAL_PRICE";
            message = "Initial price set for company.";
            return true;
        }

        if (currentPrice == newPrice)
        {
            reconciliationResult = "MATCH";
            message = "Incoming price matches current stored price.";
            return true;
        }

        decimal ratio = newPrice / currentPrice;

        // Extreme anomaly guard (> 1000% jump or > 95% drop)
        if (ratio > 10.0m || ratio < 0.05m)
        {
            reconciliationResult = "EXTREME_ANOMALY";
            message = $"Extreme price deviation rejected: current {currentPrice:N2} vs incoming {newPrice:N2} (ratio {ratio:N2}).";
            return false;
        }

        decimal pctChange = Math.Abs((newPrice - currentPrice) / currentPrice) * 100m;
        if (pctChange > 20.0m)
        {
            reconciliationResult = "SIGNIFICANT_DIFFERENCE";
            message = $"Significant price movement observed: {pctChange:N2}% change.";
            return true;
        }

        reconciliationResult = "ACCEPTABLE_DIFFERENCE";
        message = $"Price change of {pctChange:N2}% is within normal trading parameters.";
        return true;
    }
}
