namespace ShareSync.Application.Common.Models;

/// <summary>
/// Operational and network configuration options for the StockChartBD DSE Market Data API integration.
/// Bound from the "StockChartBd" configuration section in appsettings.json.
/// </summary>
public class StockChartBdOptions
{
    public const string SectionName = "StockChartBd";

    /// <summary>
    /// Base URL for the public StockChartBD REST API endpoint.
    /// Canonical default: https://stockchartbd.com/api/v1.php
    /// </summary>
    public string BaseUrl { get; set; } = "https://stockchartbd.com/api/v1.php";

    /// <summary>
    /// HTTP timeout in seconds for outgoing StockChartBD requests.
    /// Default: 10 seconds (avoids false-positive cancellations during TLS handshakes).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Conservative rolling 1-hour request quota per application instance.
    /// Documented provider limit is 120 req/hour/IP. Default is 100 to reserve 20 requests buffer.
    /// </summary>
    public int MaxRequestsPerHour { get; set; } = 100;

    /// <summary>
    /// Periodic background refresh interval in minutes.
    /// Default: 10 minutes.
    /// </summary>
    public int RefreshIntervalMinutes { get; set; } = 10;

    /// <summary>
    /// Primary server IP address for stockchartbd.com (default: 147.79.69.124).
    /// Bypasses the stale DNS A-record 88.222.243.235 which drops TCP SYN packets without responding.
    /// </summary>
    public string PrimaryServerIp { get; set; } = "147.79.69.124";

    /// <summary>
    /// Cache TTL in minutes for the 400+ company catalog (?endpoint=companies).
    /// Default: 60 minutes.
    /// </summary>
    public int CatalogCacheTtlMinutes { get; set; } = 60;

    /// <summary>
    /// Cache TTL in minutes for the market summary index/breadth payload (?endpoint=market).
    /// Default: 5 minutes.
    /// </summary>
    public int MarketSummaryCacheTtlMinutes { get; set; } = 5;

    /// <summary>
    /// Cache TTL in minutes for individual stock quotes (?endpoint=quote&symbol={symbol}).
    /// Default: 1 minute.
    /// </summary>
    public int QuoteCacheTtlMinutes { get; set; } = 1;

    /// <summary>
    /// Cache TTL in minutes for sector listings (?endpoint=sectors).
    /// Default: 60 minutes.
    /// </summary>
    public int SectorCacheTtlMinutes { get; set; } = 60;

    /// <summary>
    /// Enable secondary fallback to official Dhaka Stock Exchange portal (dse.com.bd)
    /// when an active tracked ticker is unlisted or has no trades on StockChartBD (e.g. AMCLPRAN).
    /// </summary>
    public bool EnableFallbackToDsePortal { get; set; } = true;
}
