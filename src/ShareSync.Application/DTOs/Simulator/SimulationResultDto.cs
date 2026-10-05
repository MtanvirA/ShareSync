namespace ShareSync.Application.DTOs.Simulator;

public class SimulationResultDto
{
    public int PortfolioId { get; set; }
    public string PortfolioName { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public string TransactionType { get; set; } = "BUY";
    public decimal Quantity { get; set; }
    public decimal HypotheticalPrice { get; set; }
    public decimal TotalHypotheticalAmount { get; set; }
    public decimal CurrentMarketPrice { get; set; }

    // Position Before Simulation (Current)
    public decimal CurrentHoldingQuantity { get; set; }
    public decimal CurrentAverageCost { get; set; }
    public decimal CurrentPositionCostBasis { get; set; }
    public decimal CurrentPositionMarketValue { get; set; }
    public decimal CurrentUnrealizedProfitLoss { get; set; }
    public decimal CurrentUnrealizedProfitLossPercentage { get; set; }
    public decimal CurrentAllocationPercentage { get; set; }

    // Position After Simulation
    public decimal SimulatedHoldingQuantity { get; set; }
    public decimal SimulatedAverageCost { get; set; }
    public decimal SimulatedPositionCostBasis { get; set; }
    public decimal SimulatedPositionMarketValue { get; set; }
    public decimal SimulatedUnrealizedProfitLoss { get; set; }
    public decimal SimulatedUnrealizedProfitLossPercentage { get; set; }
    public decimal SimulatedAllocationPercentage { get; set; }

    // Delta / Impact Metrics
    public decimal QuantityChange { get; set; }
    public decimal AverageCostChange { get; set; }
    public decimal PositionValueChange { get; set; }
    public decimal AllocationPercentageChange { get; set; }
    public decimal RealizedProfitLoss { get; set; }

    // Overall Portfolio Metrics Before & After
    public decimal CurrentPortfolioTotalValue { get; set; }
    public decimal SimulatedPortfolioTotalValue { get; set; }
    public decimal PortfolioTotalValueChange { get; set; }
    public decimal CurrentPortfolioTotalInvested { get; set; }
    public decimal SimulatedPortfolioTotalInvested { get; set; }
    public decimal CurrentPortfolioUnrealizedPL { get; set; }
    public decimal SimulatedPortfolioUnrealizedPL { get; set; }

    // Audit / Non-Destructive Meta
    public bool IsSimulation => true;
    public string SimulationNotice => "HYPOTHETICAL SIMULATION ONLY: No real transactions were executed, and no portfolio data was altered.";
    public DateTime SimulatedAt { get; set; } = DateTime.UtcNow;
}
