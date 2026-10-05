using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Portfolios;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Services;

public class PortfolioService : IPortfolioService
{
    private readonly IApplicationDbContext _context;

    public PortfolioService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<PortfolioSummaryDto>>> GetUserPortfoliosAsync(int userId, CancellationToken cancellationToken = default)
    {
        // Enforce user isolation: only fetch portfolios belonging to userId
        var portfolios = await _context.Portfolios
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        var portfolioIds = portfolios.Select(p => p.PortfolioId).ToList();

        // Load all transactions for these portfolios
        var transactions = await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Company)
            .Where(t => portfolioIds.Contains(t.PortfolioId))
            .ToListAsync(cancellationToken);

        var summaries = new List<PortfolioSummaryDto>();

        foreach (var p in portfolios)
        {
            var pTx = transactions.Where(t => t.PortfolioId == p.PortfolioId).ToList();
            var summary = CalculatePortfolioSummary(p, pTx);
            summaries.Add(summary);
        }

        return ApiResponse<List<PortfolioSummaryDto>>.Ok(summaries);
    }

    public async Task<ApiResponse<PortfolioDetailDto>> GetPortfolioByIdAsync(int portfolioId, int userId, CancellationToken cancellationToken = default)
    {
        var portfolio = await _context.Portfolios
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PortfolioId == portfolioId, cancellationToken);

        if (portfolio == null)
        {
            throw new NotFoundException("Portfolio", portfolioId);
        }

        // Strict ownership check: prevent cross-user access
        if (portfolio.UserId != userId)
        {
            throw new AppException("You do not have permission to access this portfolio.", 403);
        }

        var transactions = await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Company)
            .Where(t => t.PortfolioId == portfolioId)
            .ToListAsync(cancellationToken);

        var holdings = CalculateHoldings(transactions);
        var summary = CalculatePortfolioSummary(portfolio, transactions);

        var detail = new PortfolioDetailDto
        {
            PortfolioId = summary.PortfolioId,
            PortfolioName = summary.PortfolioName,
            Description = summary.Description,
            CreatedAt = summary.CreatedAt,
            TotalValue = summary.TotalValue,
            TotalInvested = summary.TotalInvested,
            UnrealizedProfitLoss = summary.UnrealizedProfitLoss,
            UnrealizedProfitLossPercentage = summary.UnrealizedProfitLossPercentage,
            HoldingsCount = summary.HoldingsCount,
            TransactionsCount = summary.TransactionsCount,
            Holdings = holdings
        };

        return ApiResponse<PortfolioDetailDto>.Ok(detail);
    }

    public async Task<ApiResponse<PortfolioSummaryDto>> CreatePortfolioAsync(CreatePortfolioRequestDto request, int userId, CancellationToken cancellationToken = default)
    {
        var trimmedName = request.PortfolioName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new AppException("Portfolio name is required.", 400);
        }

        // Check unique constraint per user: UQ_PORTFOLIOS_USER_NAME
        var exists = await _context.Portfolios
            .AnyAsync(p => p.UserId == userId && p.PortfolioName.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (exists)
        {
            throw new AppException($"A portfolio named '{trimmedName}' already exists in your account.", 409);
        }

        var portfolio = new Portfolio
        {
            UserId = userId,
            PortfolioName = trimmedName,
            Description = request.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Portfolios.Add(portfolio);
        await _context.SaveChangesAsync(cancellationToken);

        var summary = new PortfolioSummaryDto
        {
            PortfolioId = portfolio.PortfolioId,
            PortfolioName = portfolio.PortfolioName,
            Description = portfolio.Description,
            CreatedAt = portfolio.CreatedAt,
            TotalValue = 0,
            TotalInvested = 0,
            UnrealizedProfitLoss = 0,
            UnrealizedProfitLossPercentage = 0,
            HoldingsCount = 0,
            TransactionsCount = 0
        };

        return ApiResponse<PortfolioSummaryDto>.Ok(summary, "Portfolio created successfully.");
    }

    public async Task<ApiResponse<PortfolioSummaryDto>> UpdatePortfolioAsync(int portfolioId, UpdatePortfolioRequestDto request, int userId, CancellationToken cancellationToken = default)
    {
        var portfolio = await _context.Portfolios
            .FirstOrDefaultAsync(p => p.PortfolioId == portfolioId, cancellationToken);

        if (portfolio == null)
        {
            throw new NotFoundException("Portfolio", portfolioId);
        }

        // Strict ownership check
        if (portfolio.UserId != userId)
        {
            throw new AppException("You do not have permission to modify this portfolio.", 403);
        }

        var trimmedName = request.PortfolioName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new AppException("Portfolio name is required.", 400);
        }

        // Check for duplicate name under same user
        var duplicateExists = await _context.Portfolios
            .AnyAsync(p => p.UserId == userId && p.PortfolioId != portfolioId && p.PortfolioName.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (duplicateExists)
        {
            throw new AppException($"A portfolio named '{trimmedName}' already exists in your account.", 409);
        }

        portfolio.PortfolioName = trimmedName;
        portfolio.Description = request.Description?.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        var transactions = await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Company)
            .Where(t => t.PortfolioId == portfolioId)
            .ToListAsync(cancellationToken);

        var summary = CalculatePortfolioSummary(portfolio, transactions);
        return ApiResponse<PortfolioSummaryDto>.Ok(summary, "Portfolio updated successfully.");
    }

    public async Task<ApiResponse> DeletePortfolioAsync(int portfolioId, int userId, CancellationToken cancellationToken = default)
    {
        var portfolio = await _context.Portfolios
            .FirstOrDefaultAsync(p => p.PortfolioId == portfolioId, cancellationToken);

        if (portfolio == null)
        {
            throw new NotFoundException("Portfolio", portfolioId);
        }

        // Strict ownership check
        if (portfolio.UserId != userId)
        {
            throw new AppException("You do not have permission to delete this portfolio.", 403);
        }

        // Financial safety check: Cannot delete portfolio if transactions exist
        var hasTransactions = await _context.Transactions
            .AnyAsync(t => t.PortfolioId == portfolioId, cancellationToken);

        if (hasTransactions)
        {
            throw new BusinessRuleException("Cannot delete portfolio because it contains active transactions. Financial history must be preserved.");
        }

        await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Remove snapshots if any
            var snapshots = await _context.PortfolioSnapshots
                .Where(s => s.PortfolioId == portfolioId)
                .ToListAsync(cancellationToken);

            if (snapshots.Any())
            {
                _context.PortfolioSnapshots.RemoveRange(snapshots);
            }

            _context.Portfolios.Remove(portfolio);
            await _context.SaveChangesAsync(cancellationToken);

            await dbTransaction.CommitAsync(cancellationToken);
            return ApiResponse.Ok("Portfolio deleted successfully.");
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static List<PortfolioHoldingDto> CalculateHoldings(List<Transaction> transactions)
    {
        var holdings = new List<PortfolioHoldingDto>();

        var groupedByCompany = transactions
            .Where(t => t.Company != null)
            .GroupBy(t => t.CompanyId);

        foreach (var group in groupedByCompany)
        {
            var company = group.First().Company!;
            var buyTxs = group.Where(t => t.TransactionType == "BUY").ToList();
            var sellTxs = group.Where(t => t.TransactionType == "SELL").ToList();

            var totalBuyQty = buyTxs.Sum(t => t.Quantity);
            var totalSellQty = sellTxs.Sum(t => t.Quantity);
            var netQuantity = totalBuyQty - totalSellQty;

            if (netQuantity <= 0)
            {
                continue;
            }

            var totalBuyCost = buyTxs.Sum(t => t.Quantity * t.PricePerShare);
            var avgBuyPrice = totalBuyQty > 0 ? totalBuyCost / totalBuyQty : 0;
            var currentMarketValue = netQuantity * company.CurrentPrice;
            var costBasis = netQuantity * avgBuyPrice;
            var unrealizedPL = currentMarketValue - costBasis;
            var returnPct = costBasis > 0 ? (unrealizedPL / costBasis) * 100 : 0;

            holdings.Add(new PortfolioHoldingDto
            {
                CompanyId = company.CompanyId,
                CompanyName = company.CompanyName,
                TickerSymbol = company.TickerSymbol,
                Shares = netQuantity,
                AverageBuyPrice = Math.Round(avgBuyPrice, 2),
                CurrentPrice = company.CurrentPrice,
                MarketValue = Math.Round(currentMarketValue, 2),
                UnrealizedProfitLoss = Math.Round(unrealizedPL, 2),
                ReturnPercentage = Math.Round(returnPct, 2)
            });
        }

        // Calculate asset allocation percentages
        var totalMarketValue = holdings.Sum(h => h.MarketValue);
        foreach (var holding in holdings)
        {
            holding.AllocationPercentage = totalMarketValue > 0
                ? Math.Round((holding.MarketValue / totalMarketValue) * 100, 1)
                : 0;
        }

        return holdings.OrderByDescending(h => h.MarketValue).ToList();
    }

    private static PortfolioSummaryDto CalculatePortfolioSummary(Portfolio portfolio, List<Transaction> transactions)
    {
        var holdings = CalculateHoldings(transactions);

        var totalValue = holdings.Sum(h => h.MarketValue);
        var totalInvested = holdings.Sum(h => h.Shares * h.AverageBuyPrice);
        var unrealizedPL = totalValue - totalInvested;
        var plPct = totalInvested > 0 ? Math.Round((unrealizedPL / totalInvested) * 100, 2) : 0;

        return new PortfolioSummaryDto
        {
            PortfolioId = portfolio.PortfolioId,
            PortfolioName = portfolio.PortfolioName,
            Description = portfolio.Description,
            CreatedAt = portfolio.CreatedAt,
            TotalValue = Math.Round(totalValue, 2),
            TotalInvested = Math.Round(totalInvested, 2),
            UnrealizedProfitLoss = Math.Round(unrealizedPL, 2),
            UnrealizedProfitLossPercentage = plPct,
            HoldingsCount = holdings.Count,
            TransactionsCount = transactions.Count
        };
    }
}
