using ShareSync.Application.DTOs.MarketData;

namespace ShareSync.Application.Interfaces;

/// <summary>
/// Centralized, rate-limit-aware client for all StockChartBD public REST API endpoints.
/// Handles endpoint construction, HTTP retry, DNS failover, caching, deserialization, and diagnostics.
/// Canonical Base URL: https://stockchartbd.com/api/v1.php
/// </summary>
public interface IStockChartBdClient
{
    /// <summary>
    /// Fetches market indices (DSEX, DSES, DS30, CDSET) and market breadth (?endpoint=market).
    /// Cached with configurable TTL (default 5 mins).
    /// </summary>
    Task<StockChartBdMarketSummaryDto?> GetMarketSummaryAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the complete 400+ listed company directory (?endpoint=companies).
    /// Cached with configurable TTL (default 60 mins).
    /// </summary>
    Task<List<DseListedCompanyDto>?> GetCompaniesAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the latest stock quote for a specific ticker symbol (?endpoint=quote&symbol={symbol}).
    /// Automatically resolves ticker aliases (e.g. SQUAREPHARMA <-> SQURPHARMA).
    /// Falls back to official DSE exchange portal if ticker is illiquid or unlisted on StockChartBD.
    /// </summary>
    Task<DseQuoteData?> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches historical daily OHLCV records for a specific company (?endpoint=history&symbol={symbol}&limit={limit}).
    /// Up to 1,000 observations per request.
    /// </summary>
    Task<List<StockChartBdHistoricalPriceDto>?> GetHistoricalPricesAsync(string symbol, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches yearly and interim financial statements for a listed company (?endpoint=financials&symbol={symbol}).
    /// </summary>
    Task<StockChartBdFinancialsDto?> GetFinancialsAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches company financial health score and Z-category warning signals (?endpoint=health&symbol={symbol}).
    /// Note: This is StockChartBD's exchange health classification, distinct from ShareSync's Investment Intelligence Score.
    /// </summary>
    Task<StockChartBdHealthDto?> GetHealthAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches market sector directory with company counts and aggregate market cap (?endpoint=sectors).
    /// Cached with configurable TTL (default 60 mins).
    /// </summary>
    Task<List<StockChartBdSectorDto>?> GetSectorsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns live operational metrics including rate-limit budget, cached items, and connection status.
    /// </summary>
    StockChartBdDiagnosticsDto GetDiagnostics();
}
