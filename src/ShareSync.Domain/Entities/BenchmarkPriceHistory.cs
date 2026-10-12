namespace ShareSync.Domain.Entities;

/// <summary>
/// FEATURE: Benchmark Comparison (LF-30)
/// BENCHMARK: Historical Data
/// DATABASE: Table — benchmark_price_history
/// 
/// PURPOSE:
/// Persists canonical historical index time-series (DSEX, DS30, DSES) directly in MySQL.
/// Enables fast, reliable outperformance comparisons between investor portfolios and
/// reference market benchmarks over any arbitrary historical date window.
/// 
/// RELATIONAL MAPPING:
/// - Primary Key: BenchmarkPriceHistoryId (AUTO_INCREMENT in MySQL)
/// - Unique Index: (BenchmarkCode, TradingDate) via uq_bench_code_date
/// - Range Index: idx_bench_code_date for sub-millisecond date interval scans
/// </summary>
public class BenchmarkPriceHistory
{
    /// <summary>
    /// DATABASE: Primary key identifier.
    /// </summary>
    public int BenchmarkPriceHistoryId { get; set; }

    /// <summary>
    /// BENCHMARK: Index code symbol ("DSEX", "DS30", "DSES").
    /// </summary>
    public string BenchmarkCode { get; set; } = string.Empty;

    /// <summary>
    /// Formal index title (e.g. "DSE Broad Index (DSEX)", "DSE 30 Index (DS30)").
    /// </summary>
    public string BenchmarkName { get; set; } = string.Empty;

    /// <summary>
    /// Observation market trading date (YYYY-MM-DD).
    /// </summary>
    public DateTime TradingDate { get; set; }

    /// <summary>
    /// BENCHMARK: Index closing level used for return calculations.
    /// </summary>
    public decimal CloseValue { get; set; }

    /// <summary>
    /// Daily opening index level (optional).
    /// </summary>
    public decimal? OpenValue { get; set; }

    /// <summary>
    /// Intraday high level (optional).
    /// </summary>
    public decimal? HighValue { get; set; }

    /// <summary>
    /// Intraday low level (optional).
    /// </summary>
    public decimal? LowValue { get; set; }

    /// <summary>
    /// Total aggregate trading volume across all constituents (optional).
    /// </summary>
    public long? Volume { get; set; }

    /// <summary>
    /// DATA QUALITY: Provenance
    /// Institutional data provider source (e.g., "Dhaka Stock Exchange").
    /// </summary>
    public string Source { get; set; } = "Dhaka Stock Exchange";

    /// <summary>
    /// Academic dataset identifier (e.g., "Harvard Dataverse DSE Archive").
    /// </summary>
    public string? SourceDataset { get; set; }

    /// <summary>
    /// Permanent Digital Object Identifier citing data provenance.
    /// </summary>
    public string? SourceDoi { get; set; }

    /// <summary>
    /// Batch import execution identifier for audit traceability.
    /// </summary>
    public string? ImportBatchId { get; set; }

    /// <summary>
    /// Record creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

