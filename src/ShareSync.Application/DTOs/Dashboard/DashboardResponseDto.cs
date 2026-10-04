namespace ShareSync.Application.DTOs.Dashboard;

public class DashboardSummaryDto
{
    public decimal TotalPortfolioValue { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal TotalUnrealizedProfitLoss { get; set; }
    public decimal ProfitLossPercentage { get; set; }
    public decimal TotalDividendIncome { get; set; }
    public int PortfoliosCount { get; set; }
    public int HoldingsCount { get; set; }
}

public class DashboardRecentTransactionDto
{
    public int TransactionId { get; set; }
    public int PortfolioId { get; set; }
    public string PortfolioName { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty; // BUY / SELL
    public decimal Quantity { get; set; }
    public decimal PricePerShare { get; set; }
    public decimal TotalValue { get; set; }
    public DateTime TransactionDate { get; set; }
}

public class DashboardTopHoldingDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public string SectorName { get; set; } = string.Empty;
    public decimal Shares { get; set; }
    public decimal AverageBuyPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal MarketValue { get; set; }
    public decimal UnrealizedProfitLoss { get; set; }
    public decimal ReturnPercentage { get; set; }
    public decimal AllocationPercentage { get; set; }
}

public class DashboardPerformanceDto
{
    public decimal StartingValue { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal NetChange { get; set; }
    public decimal NetChangePercentage { get; set; }
    public List<string> Labels { get; set; } = new();
    public List<decimal> Values { get; set; } = new();
}

public class DashboardSectorAllocationDto
{
    public int SectorId { get; set; }
    public string SectorName { get; set; } = string.Empty;
    public int HoldingsCount { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal MarketValue { get; set; }
    public decimal AllocationPercentage { get; set; }
}

public class DashboardResponseDto
{
    public string UserFullName { get; set; } = string.Empty;
    public DashboardSummaryDto Summary { get; set; } = new();
    public List<DashboardRecentTransactionDto> RecentTransactions { get; set; } = new();
    public List<DashboardTopHoldingDto> TopHoldings { get; set; } = new();
    public DashboardPerformanceDto Performance { get; set; } = new();
    public List<DashboardSectorAllocationDto> SectorAllocation { get; set; } = new();
}
