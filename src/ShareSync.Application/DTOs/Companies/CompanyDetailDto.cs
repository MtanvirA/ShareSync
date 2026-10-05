namespace ShareSync.Application.DTOs.Companies;

public class CompanyDetailDto
{
    // 1. Shared Public Reference Data
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public int SectorId { get; set; }
    public string SectorName { get; set; } = string.Empty;
    public string Sector => SectorName;
    public decimal CurrentPrice { get; set; }
    public decimal? MarketCap { get; set; }

    // 2. Quick Market Performance Stats (derived from historical snapshots)
    public decimal? YearHigh { get; set; }
    public decimal? YearLow { get; set; }
    public decimal? DayChange { get; set; }
    public decimal? DayChangePercentage { get; set; }

    // 3. User-Specific Holding / Position (Private to Authenticated User)
    public bool UserOwnsShares => UserHolding != null && UserHolding.Shares > 0;
    public CompanyUserPositionDto? UserHolding { get; set; }
    public CompanyUserPositionDto? UserPosition => UserHolding;

    // 4. User-Specific Watchlist Status (Private to Authenticated User)
    public bool IsInWatchlist { get; set; }
    public int? WatchlistId { get; set; }
    public string? WatchlistName { get; set; }
    public decimal? TargetPrice { get; set; }
    public decimal? TargetDistancePercentage { get; set; }
    public bool IsTargetReached { get; set; }

    // 5. Dividend History with User-Specific Estimated Income
    public List<CompanyDividendItemDto> Dividends { get; set; } = new();
    public List<CompanyDividendItemDto> DividendHistory => Dividends;
}

public class CompanyUserPositionDto
{
    public decimal Shares { get; set; }
    public decimal TotalShares => Shares;
    public decimal AverageBuyPrice { get; set; }
    public decimal AverageCost => AverageBuyPrice;
    public decimal CostBasis { get; set; }
    public decimal CurrentMarketValue { get; set; }
    public decimal CurrentValue => CurrentMarketValue;
    public decimal UnrealizedProfitLoss { get; set; }
    public decimal ReturnPercentage { get; set; }
    public decimal UnrealizedProfitLossPercentage => ReturnPercentage;
    public List<CompanyPortfolioBreakdownDto> PortfolioBreakdown { get; set; } = new();
}

public class CompanyPortfolioBreakdownDto
{
    public int PortfolioId { get; set; }
    public string PortfolioName { get; set; } = string.Empty;
    public decimal Shares { get; set; }
    public decimal AverageBuyPrice { get; set; }
    public decimal MarketValue { get; set; }
    public decimal UnrealizedProfitLoss { get; set; }
    public decimal ReturnPercentage { get; set; }
}

public class CompanyDividendItemDto
{
    public int DividendId { get; set; }
    public decimal DividendPerShare { get; set; }
    public DateTime DeclarationDate { get; set; }
    public DateTime PaymentDate { get; set; }
    public string Status { get; set; } = "Paid";
    public decimal UserSharesHeld { get; set; }
    public decimal EstimatedIncome { get; set; }
    public decimal? EstimatedUserIncome => EstimatedIncome;
}
