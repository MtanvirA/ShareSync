using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Simulator;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Services;

public class SimulatorService : ISimulatorService
{
    private readonly IApplicationDbContext _context;

    public SimulatorService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<SimulationResultDto>> SimulateTransactionAsync(
        SimulateTransactionRequestDto request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        // 1. Authenticated user validation
        if (userId <= 0)
        {
            throw new UnauthorizedException("User session is invalid.");
        }

        if (request == null)
        {
            throw new AppException("Simulation request payload cannot be empty.", 400);
        }

        // 1. Transaction type validation
        var txType = request.TransactionType?.Trim().ToUpperInvariant();
        if (txType != "BUY" && txType != "SELL")
        {
            throw new AppException("Transaction type must be either 'BUY' or 'SELL'.", 400);
        }

        // 2. Quantity validation
        if (request.Quantity <= 0)
        {
            throw new AppException("Quantity must be greater than zero.", 400);
        }

        if (request.Quantity != Math.Floor(request.Quantity))
        {
            throw new AppException("Fractional shares are not supported. Quantity must be a whole integer.", 400);
        }

        // 3. Hypothetical price validation
        if (request.HypotheticalPrice <= 0)
        {
            throw new AppException("Hypothetical price per share must be greater than zero.", 400);
        }

        // 4. Portfolio existence and strict ownership check
        var portfolio = await _context.Portfolios
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PortfolioId == request.PortfolioId, cancellationToken);

        if (portfolio == null)
        {
            throw new NotFoundException("Portfolio", request.PortfolioId);
        }

        if (portfolio.UserId != userId)
        {
            throw new AppException("You do not have permission to access this portfolio.", 403);
        }

        // 5. Company existence check
        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == request.CompanyId, cancellationToken);

        if (company == null)
        {
            throw new NotFoundException("Company", request.CompanyId);
        }

        // 6. Load all existing transactions for this portfolio (read-only)
        var transactions = await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Company)
            .Where(t => t.PortfolioId == request.PortfolioId && t.Company != null)
            .ToListAsync(cancellationToken);

        // 8. Authoritative calculation of current holdings across the portfolio
        var grouped = transactions.GroupBy(t => t.CompanyId);

        decimal currentPortfolioTotalMarketVal = 0m;
        decimal currentPortfolioTotalInvestedVal = 0m;

        decimal currentHoldingQty = 0m;
        decimal currentAvgBuyPrice = 0m;
        decimal currentMarketVal = 0m;
        decimal currentInvestedVal = 0m;
        decimal currentUnrealizedPL = 0m;
        decimal currentReturnPct = 0m;

        decimal otherHoldingsMarketVal = 0m;
        decimal otherHoldingsInvestedVal = 0m;

        foreach (var g in grouped)
        {
            var comp = g.First().Company!;
            var buyTxs = g.Where(t => t.TransactionType == "BUY").ToList();
            var sellTxs = g.Where(t => t.TransactionType == "SELL").ToList();

            var buyQty = buyTxs.Sum(t => t.Quantity);
            var sellQty = sellTxs.Sum(t => t.Quantity);
            var netQty = buyQty - sellQty;

            if (netQty <= 0) continue;

            var totalBuyCost = buyTxs.Sum(t => t.Quantity * t.PricePerShare);
            var avgBuy = buyQty > 0 ? totalBuyCost / buyQty : 0m;
            var mVal = netQty * comp.CurrentPrice;
            var invVal = netQty * avgBuy;

            currentPortfolioTotalMarketVal += mVal;
            currentPortfolioTotalInvestedVal += invVal;

            if (g.Key == company.CompanyId)
            {
                currentHoldingQty = netQty;
                currentAvgBuyPrice = avgBuy;
                currentMarketVal = mVal;
                currentInvestedVal = invVal;
                currentUnrealizedPL = mVal - invVal;
                currentReturnPct = invVal > 0 ? (currentUnrealizedPL / invVal) * 100 : 0m;
            }
            else
            {
                otherHoldingsMarketVal += mVal;
                otherHoldingsInvestedVal += invVal;
            }
        }

        decimal currentAllocPct = currentPortfolioTotalMarketVal > 0
            ? Math.Round((currentMarketVal / currentPortfolioTotalMarketVal) * 100, 2)
            : 0m;

        // 9. Evaluate hypothetical transaction
        decimal simulatedNetQty;
        decimal simulatedAvgCost;
        decimal simulatedMarketVal;
        decimal simulatedInvestedVal;
        decimal simulatedUnrealizedPL;
        decimal simulatedReturnPct;
        decimal realizedPL = 0m;

        if (txType == "SELL")
        {
            // Oversell prevention in simulation
            if (request.Quantity > currentHoldingQty)
            {
                throw new BusinessRuleException(
                    $"Cannot simulate SELL order: Requested quantity ({request.Quantity:N2}) exceeds available holdings ({currentHoldingQty:N2}) of {company.TickerSymbol}."
                );
            }

            simulatedNetQty = currentHoldingQty - request.Quantity;
            simulatedAvgCost = currentAvgBuyPrice; // Weighted average cost of remaining shares is unchanged
            simulatedMarketVal = simulatedNetQty * company.CurrentPrice;
            simulatedInvestedVal = simulatedNetQty * simulatedAvgCost;
            simulatedUnrealizedPL = simulatedMarketVal - simulatedInvestedVal;
            simulatedReturnPct = simulatedInvestedVal > 0 ? (simulatedUnrealizedPL / simulatedInvestedVal) * 100 : 0m;
            realizedPL = Math.Round((request.HypotheticalPrice - currentAvgBuyPrice) * request.Quantity, 2);
        }
        else
        {
            // BUY simulation
            var existingBuyTxs = transactions.Where(t => t.CompanyId == company.CompanyId && t.TransactionType == "BUY").ToList();
            var existingBuyQty = existingBuyTxs.Sum(t => t.Quantity);
            var existingBuyCost = existingBuyTxs.Sum(t => t.Quantity * t.PricePerShare);

            var totalSimulatedBuyQty = existingBuyQty + request.Quantity;
            var totalSimulatedBuyCost = existingBuyCost + (request.Quantity * request.HypotheticalPrice);

            simulatedNetQty = currentHoldingQty + request.Quantity;
            simulatedAvgCost = totalSimulatedBuyQty > 0 ? totalSimulatedBuyCost / totalSimulatedBuyQty : 0m;
            simulatedMarketVal = simulatedNetQty * company.CurrentPrice;
            simulatedInvestedVal = simulatedNetQty * simulatedAvgCost;
            simulatedUnrealizedPL = simulatedMarketVal - simulatedInvestedVal;
            simulatedReturnPct = simulatedInvestedVal > 0 ? (simulatedUnrealizedPL / simulatedInvestedVal) * 100 : 0m;
            realizedPL = 0m;
        }

        // 10. Portfolio-level simulation metrics
        decimal simulatedPortfolioTotalVal = otherHoldingsMarketVal + simulatedMarketVal;
        decimal simulatedPortfolioTotalInv = otherHoldingsInvestedVal + simulatedInvestedVal;
        decimal simulatedPortfolioUnrealizedPL = simulatedPortfolioTotalVal - simulatedPortfolioTotalInv;

        decimal simulatedAllocPct = simulatedPortfolioTotalVal > 0
            ? Math.Round((simulatedMarketVal / simulatedPortfolioTotalVal) * 100, 2)
            : 0m;

        decimal totalSimulatedAmount = Math.Round(request.Quantity * request.HypotheticalPrice, 2);

        var result = new SimulationResultDto
        {
            PortfolioId = portfolio.PortfolioId,
            PortfolioName = portfolio.PortfolioName,
            CompanyId = company.CompanyId,
            CompanyName = company.CompanyName,
            TickerSymbol = company.TickerSymbol,
            TransactionType = txType,
            Quantity = request.Quantity,
            HypotheticalPrice = Math.Round(request.HypotheticalPrice, 2),
            TotalHypotheticalAmount = totalSimulatedAmount,
            CurrentMarketPrice = Math.Round(company.CurrentPrice, 2),

            // Current state
            CurrentHoldingQuantity = currentHoldingQty,
            CurrentAverageCost = Math.Round(currentAvgBuyPrice, 2),
            CurrentPositionCostBasis = Math.Round(currentInvestedVal, 2),
            CurrentPositionMarketValue = Math.Round(currentMarketVal, 2),
            CurrentUnrealizedProfitLoss = Math.Round(currentUnrealizedPL, 2),
            CurrentUnrealizedProfitLossPercentage = Math.Round(currentReturnPct, 2),
            CurrentAllocationPercentage = currentAllocPct,

            // Simulated state
            SimulatedHoldingQuantity = simulatedNetQty,
            SimulatedAverageCost = Math.Round(simulatedAvgCost, 2),
            SimulatedPositionCostBasis = Math.Round(simulatedInvestedVal, 2),
            SimulatedPositionMarketValue = Math.Round(simulatedMarketVal, 2),
            SimulatedUnrealizedProfitLoss = Math.Round(simulatedUnrealizedPL, 2),
            SimulatedUnrealizedProfitLossPercentage = Math.Round(simulatedReturnPct, 2),
            SimulatedAllocationPercentage = simulatedAllocPct,

            // Deltas
            QuantityChange = txType == "BUY" ? request.Quantity : -request.Quantity,
            AverageCostChange = Math.Round(simulatedAvgCost - currentAvgBuyPrice, 2),
            PositionValueChange = Math.Round(simulatedMarketVal - currentMarketVal, 2),
            AllocationPercentageChange = Math.Round(simulatedAllocPct - currentAllocPct, 2),
            RealizedProfitLoss = realizedPL,

            // Portfolio-level
            CurrentPortfolioTotalValue = Math.Round(currentPortfolioTotalMarketVal, 2),
            SimulatedPortfolioTotalValue = Math.Round(simulatedPortfolioTotalVal, 2),
            PortfolioTotalValueChange = Math.Round(simulatedPortfolioTotalVal - currentPortfolioTotalMarketVal, 2),
            CurrentPortfolioTotalInvested = Math.Round(currentPortfolioTotalInvestedVal, 2),
            SimulatedPortfolioTotalInvested = Math.Round(simulatedPortfolioTotalInv, 2),
            CurrentPortfolioUnrealizedPL = Math.Round(currentPortfolioTotalMarketVal - currentPortfolioTotalInvestedVal, 2),
            SimulatedPortfolioUnrealizedPL = Math.Round(simulatedPortfolioUnrealizedPL, 2),
            SimulatedAt = DateTime.UtcNow
        };

        return ApiResponse<SimulationResultDto>.Ok(result, "Hypothetical simulation calculated successfully. Real records remain unchanged.");
    }
}
