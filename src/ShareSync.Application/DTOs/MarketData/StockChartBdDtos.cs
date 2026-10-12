using System.Text.Json.Serialization;

namespace ShareSync.Application.DTOs.MarketData;

/// <summary>
/// Data transfer models for the StockChartBD public market-data API endpoints.
/// Supports market summary, listed companies, quotes, historical prices, financials, health, and sectors.
/// </summary>

// =========================================================================
// 1. MARKET SUMMARY (?endpoint=market)
// =========================================================================

public class StockChartBdMarketSummaryDto
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("indices")]
    public Dictionary<string, StockChartBdIndexDto> Indices { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("breadth")]
    public StockChartBdBreadthDto? Breadth { get; set; }

    [JsonPropertyName("meta")]
    public StockChartBdMetaDto? Meta { get; set; }

    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;
    public bool IsCached { get; set; }
}

public class StockChartBdIndexDto
{
    [JsonPropertyName("close")]
    public decimal Close { get; set; }

    [JsonPropertyName("change")]
    public decimal? Change { get; set; }

    [JsonPropertyName("change_percent")]
    public decimal? ChangePercent { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }
}

public class StockChartBdBreadthDto
{
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("advanced")]
    public int Advanced { get; set; }

    [JsonPropertyName("declined")]
    public int Declined { get; set; }

    [JsonPropertyName("unchanged")]
    public int Unchanged { get; set; }

    [JsonPropertyName("total_volume")]
    public long? TotalVolume { get; set; }

    [JsonPropertyName("total_value_mn")]
    public decimal? TotalValueMn { get; set; }

    [JsonPropertyName("total_trades")]
    public long? TotalTrades { get; set; }
}

public class StockChartBdMetaDto
{
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("as_of")]
    public string? AsOf { get; set; }

    [JsonPropertyName("disclaimer")]
    public string? Disclaimer { get; set; }
}

// =========================================================================
// 2. INDIVIDUAL STOCK QUOTE (?endpoint=quote&symbol={symbol})
// =========================================================================

public class StockChartBdQuoteResponseDto
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("quote")]
    public StockChartBdQuoteDetailsDto? Quote { get; set; }

    [JsonPropertyName("meta")]
    public StockChartBdMetaDto? Meta { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class StockChartBdQuoteDetailsDto
{
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("open")]
    public decimal Open { get; set; }

    [JsonPropertyName("high")]
    public decimal High { get; set; }

    [JsonPropertyName("low")]
    public decimal Low { get; set; }

    [JsonPropertyName("close")]
    public decimal Close { get; set; }

    [JsonPropertyName("previous_close")]
    public decimal? PreviousClose { get; set; }

    [JsonPropertyName("change")]
    public decimal? Change { get; set; }

    [JsonPropertyName("change_percent")]
    public decimal? ChangePercent { get; set; }

    [JsonPropertyName("volume")]
    public long? Volume { get; set; }

    [JsonPropertyName("trades")]
    public long? Trades { get; set; }

    [JsonPropertyName("value_mn")]
    public decimal? ValueMn { get; set; }
}

// =========================================================================
// 3. HISTORICAL PRICES (?endpoint=history&symbol={symbol}&limit={limit})
// =========================================================================

public class StockChartBdHistoryResponseDto
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("history")]
    public List<StockChartBdHistoricalPriceDto> History { get; set; } = new();

    [JsonPropertyName("meta")]
    public StockChartBdMetaDto? Meta { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class StockChartBdHistoricalPriceDto
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("open")]
    public decimal Open { get; set; }

    [JsonPropertyName("high")]
    public decimal High { get; set; }

    [JsonPropertyName("low")]
    public decimal Low { get; set; }

    [JsonPropertyName("close")]
    public decimal Close { get; set; }

    [JsonPropertyName("volume")]
    public long? Volume { get; set; }

    [JsonPropertyName("trades")]
    public long? Trades { get; set; }

    [JsonPropertyName("value_mn")]
    public decimal? ValueMn { get; set; }
}

// =========================================================================
// 4. FINANCIAL DATA (?endpoint=financials&symbol={symbol})
// =========================================================================

public class StockChartBdFinancialsDto
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("yearly")]
    public List<StockChartBdYearlyFinancialDto> Yearly { get; set; } = new();

    [JsonPropertyName("interim")]
    public List<StockChartBdInterimFinancialDto> Interim { get; set; } = new();

    [JsonPropertyName("meta")]
    public StockChartBdMetaDto? Meta { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class StockChartBdYearlyFinancialDto
{
    [JsonPropertyName("year")]
    public string Year { get; set; } = string.Empty;

    [JsonPropertyName("eps")]
    public decimal? Eps { get; set; }

    [JsonPropertyName("nav_per_share")]
    public decimal? NavPerShare { get; set; }

    [JsonPropertyName("profit_mn")]
    public decimal? ProfitMn { get; set; }

    [JsonPropertyName("pe_at_year_end")]
    public decimal? PeAtYearEnd { get; set; }

    [JsonPropertyName("dividend_percent")]
    public decimal? DividendPercent { get; set; }

    [JsonPropertyName("dividend_yield_percent")]
    public decimal? DividendYieldPercent { get; set; }
}

public class StockChartBdInterimFinancialDto
{
    [JsonPropertyName("period")]
    public string Period { get; set; } = string.Empty;

    [JsonPropertyName("ends_on")]
    public string EndsOn { get; set; } = string.Empty;

    [JsonPropertyName("eps")]
    public decimal? Eps { get; set; }

    [JsonPropertyName("revenue_mn")]
    public decimal? RevenueMn { get; set; }

    [JsonPropertyName("profit_mn")]
    public decimal? ProfitMn { get; set; }
}

// =========================================================================
// 5. COMPANY HEALTH DATA (?endpoint=health&symbol={symbol})
// =========================================================================

public class StockChartBdHealthDto
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("health")]
    public StockChartBdHealthScoreDto? Health { get; set; }

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("meta")]
    public StockChartBdMetaDto? Meta { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class StockChartBdHealthScoreDto
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("band")]
    public string Band { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("signals")]
    public List<StockChartBdHealthSignalDto> Signals { get; set; } = new();
}

public class StockChartBdHealthSignalDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("points")]
    public int Points { get; set; }

    [JsonPropertyName("is_dse_criterion")]
    public bool IsDseCriterion { get; set; }
}

// =========================================================================
// 6. SECTORS (?endpoint=sectors)
// =========================================================================

public class StockChartBdSectorsResponseDto
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("sectors")]
    public List<StockChartBdSectorDto> Sectors { get; set; } = new();

    [JsonPropertyName("meta")]
    public StockChartBdMetaDto? Meta { get; set; }
}

public class StockChartBdSectorDto
{
    [JsonPropertyName("sector")]
    public string Sector { get; set; } = string.Empty;

    [JsonPropertyName("companies")]
    public int Companies { get; set; }

    [JsonPropertyName("market_cap_mn")]
    public decimal? MarketCapMn { get; set; }
}

// =========================================================================
// 7. DIAGNOSTICS & TELEMETRY
// =========================================================================

public class StockChartBdDiagnosticsDto
{
    public string BaseUrl { get; set; } = string.Empty;
    public int RequestsUsedInLastHour { get; set; }
    public int HourlyBudget { get; set; }
    public int RemainingHourlyBudget { get; set; }
    public bool IsThrottled { get; set; }
    public DateTime? ThrottleResetAt { get; set; }
    public DateTime? LastSuccessfulRequestAt { get; set; }
    public string? LastEndpointQueried { get; set; }
    public int CachedCompaniesCount { get; set; }
    public int CachedQuotesCount { get; set; }
    public bool MarketSummaryCached { get; set; }
    public bool FallbackToDsePortalActive { get; set; }
}
