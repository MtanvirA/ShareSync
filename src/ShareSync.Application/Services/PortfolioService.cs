using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Portfolios;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Services;

/// <summary>
/// FEATURE: Multi-Portfolio Management
/// FEATURE: Multi-Asset and Multi-Currency (LF-29)
/// HOLDINGS: Holding Valuation Engine
/// 
/// PURPOSE:
/// Manages user portfolios, aggregates transaction lots into net holdings positions,
/// calculates mark-to-market valuations, handles foreign currency exchange rate conversions,
/// computes unrealized and realized P/L, and determines portfolio-level asset allocation percentages.
/// </summary>
public class PortfolioService : IPortfolioService
{
    private readonly IApplicationDbContext _context;
    private readonly IInflationService _inflationService;
    private readonly ICurrencyService _currencyService;

    public PortfolioService(IApplicationDbContext context)
        : this(context, new InflationService(context), new CurrencyService(context))
    {
    }

    public PortfolioService(
        IApplicationDbContext context,
        IInflationService inflationService,
        ICurrencyService currencyService)
    {
        _context = context;
        _inflationService = inflationService;
        _currencyService = currencyService;
    }

    /// <summary>
    /// API: Get User Portfolios
    /// SECURITY: Ownership Isolation
    /// 
    /// PURPOSE:
    /// Retrieves all portfolios belonging to the authenticated user with aggregate performance metrics,
    /// total values, realized and unrealized P/L, and inflation-adjusted real returns.
    /// 
    /// INPUT: userId
    /// OUTPUT: List of PortfolioSummaryDto
    /// </summary>
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

        var exchangeRates = await _currencyService.GetAllRatesAsync(cancellationToken);
        var summaries = new List<PortfolioSummaryDto>();

        foreach (var p in portfolios)
        {
            var pTx = transactions.Where(t => t.PortfolioId == p.PortfolioId).ToList();
            var summary = await CalculatePortfolioSummaryAsync(p, pTx, exchangeRates, cancellationToken);
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

        var exchangeRates = await _currencyService.GetAllRatesAsync(cancellationToken);
        var holdings = CalculateHoldings(transactions, portfolio.Currency ?? "BDT", exchangeRates);
        var summary = await CalculatePortfolioSummaryAsync(portfolio, transactions, exchangeRates, cancellationToken);

        var detail = new PortfolioDetailDto
        {
            PortfolioId = summary.PortfolioId,
            PortfolioName = summary.PortfolioName,
            Description = summary.Description,
            CreatedAt = summary.CreatedAt,
            Currency = summary.Currency,
            TotalValue = summary.TotalValue,
            TotalInvested = summary.TotalInvested,
            UnrealizedProfitLoss = summary.UnrealizedProfitLoss,
            UnrealizedProfitLossPercentage = summary.UnrealizedProfitLossPercentage,
            RealizedProfitLoss = summary.RealizedProfitLoss,
            TotalFees = summary.TotalFees,
            NetCashFlow = summary.NetCashFlow,
            InflationRate = summary.InflationRate,
            RealReturnPercentage = summary.RealReturnPercentage,
            InflationStatus = summary.InflationStatus,
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

        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "BDT" : request.Currency.Trim().ToUpperInvariant();
        if (currency != "BDT" && currency != "USD")
        {
            throw new AppException($"Unsupported portfolio currency '{request.Currency}'. Supported currencies: BDT, USD.", 400);
        }

        var portfolio = new Portfolio
        {
            UserId = userId,
            PortfolioName = trimmedName,
            Description = request.Description?.Trim(),
            Currency = currency,
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
            Currency = portfolio.Currency,
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

        var exchangeRates = await _currencyService.GetAllRatesAsync(cancellationToken);
        var summary = await CalculatePortfolioSummaryAsync(portfolio, transactions, exchangeRates, cancellationToken);
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

    /// <summary>
    /// HOLDINGS: Position Aggregation &amp; Valuation
    /// CALCULATION: Weighted Average Cost
    /// CALCULATION: Unrealized P/L
    /// CALCULATION: Realized P/L
    /// CALCULATION: Currency Conversion
    /// CALCULATION: Asset Allocation
    /// 
    /// FORMULAS:
    /// 1. Net Shares = SUM(BUY quantity) - SUM(SELL quantity)
    /// 2. Weighted Average Buy Price = SUM(BUY quantity * BUY price) / SUM(BUY quantity)
    /// 3. Local Market Value = Net Shares * CurrentPrice
    /// 4. Base Market Value = Local Market Value * ExchangeRate (if asset currency != portfolio base currency)
    /// 5. Unrealized P/L = (CurrentPrice - Weighted Average Buy Price) * Net Shares * ExchangeRate
    /// 6. Realized P/L = SUM((SELL qty * SELL price - Fee) - (SELL qty * Weighted Average Buy Price)) * ExchangeRate
    /// 7. Allocation % = (Holding Base Market Value / Total Portfolio Market Value) * 100
    /// </summary>
    private static List<PortfolioHoldingDto> CalculateHoldings(
        List<Transaction> transactions,
        string baseCurrency,
        List<DTOs.Currencies.ExchangeRateDto> exchangeRates)
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

            // Multi-currency conversion to portfolio base currency
            var assetCurrency = string.IsNullOrWhiteSpace(company.Currency) ? "BDT" : company.Currency;
            decimal fxRate = 1.0m;
            if (!string.Equals(assetCurrency, baseCurrency, StringComparison.OrdinalIgnoreCase))
            {
                var rateEntry = exchangeRates.FirstOrDefault(r =>
                    r.FromCurrency.Equals(assetCurrency, StringComparison.OrdinalIgnoreCase) &&
                    r.ToCurrency.Equals(baseCurrency, StringComparison.OrdinalIgnoreCase));

                if (rateEntry != null)
                {
                    fxRate = rateEntry.Rate;
                }
            }

            var baseMarketValue = currentMarketValue * fxRate;

            // Realized P/L for shares of this company sold in this portfolio
            decimal companyRealizedPL = 0m;
            foreach (var s in sellTxs)
            {
                companyRealizedPL += (s.Quantity * s.PricePerShare - s.Fee) - (s.Quantity * avgBuyPrice);
            }

            holdings.Add(new PortfolioHoldingDto
            {
                CompanyId = company.CompanyId,
                CompanyName = company.CompanyName,
                TickerSymbol = company.TickerSymbol,
                AssetType = company.AssetType ?? "EQUITY",
                Currency = assetCurrency,
                ExchangeRate = fxRate,
                Shares = netQuantity,
                AverageBuyPrice = Math.Round(avgBuyPrice, 2),
                CurrentPrice = company.CurrentPrice,
                LocalMarketValue = Math.Round(currentMarketValue, 2),
                BaseMarketValue = Math.Round(baseMarketValue, 2),
                MarketValue = Math.Round(baseMarketValue, 2), // standard display in portfolio currency
                UnrealizedProfitLoss = Math.Round(unrealizedPL * fxRate, 2),
                RealizedProfitLoss = Math.Round(companyRealizedPL * fxRate, 2),
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

    private async Task<PortfolioSummaryDto> CalculatePortfolioSummaryAsync(
        Portfolio portfolio,
        List<Transaction> transactions,
        List<DTOs.Currencies.ExchangeRateDto> exchangeRates,
        CancellationToken cancellationToken)
    {
        var baseCurrency = portfolio.Currency ?? "BDT";
        var holdings = CalculateHoldings(transactions, baseCurrency, exchangeRates);

        var totalValue = holdings.Sum(h => h.MarketValue);
        // CALCULATION: Portfolio Value & Net Unrealized P/L
        // Aggregates holdings market values and exact holdings unrealized profit/loss.
        // Derives total invested capital as (totalValue - unrealizedPL) to avoid premature
        // rounding errors that occur when multiplying shares by 2-decimal rounded average buy price.
        var unrealizedPL = holdings.Sum(h => h.UnrealizedProfitLoss);
        var totalInvested = totalValue - unrealizedPL;
        var plPct = totalInvested > 0 ? Math.Round((unrealizedPL / totalInvested) * 100, 2) : 0;

        // Calculate Realized P/L across ALL sell transactions in the portfolio (including closed positions)
        decimal totalRealizedPL = 0m;
        var groupedByComp = transactions.GroupBy(t => t.CompanyId);
        foreach (var group in groupedByComp)
        {
            var buyTxs = group.Where(t => t.TransactionType == "BUY").ToList();
            var sellTxs = group.Where(t => t.TransactionType == "SELL").ToList();
            if (!sellTxs.Any()) continue;

            var comp = group.First().Company;
            var compCurrency = comp?.Currency ?? "BDT";
            decimal fxRate = 1.0m;
            if (!string.Equals(compCurrency, baseCurrency, StringComparison.OrdinalIgnoreCase))
            {
                var rateEntry = exchangeRates.FirstOrDefault(r =>
                    r.FromCurrency.Equals(compCurrency, StringComparison.OrdinalIgnoreCase) &&
                    r.ToCurrency.Equals(baseCurrency, StringComparison.OrdinalIgnoreCase));
                if (rateEntry != null) fxRate = rateEntry.Rate;
            }

            var totalBuyQty = buyTxs.Sum(t => t.Quantity);
            var totalBuyCost = buyTxs.Sum(t => t.Quantity * t.PricePerShare);
            var avgBuyPrice = totalBuyQty > 0 ? totalBuyCost / totalBuyQty : 0m;

            foreach (var s in sellTxs)
            {
                var pnl = (s.Quantity * s.PricePerShare - s.Fee) - (s.Quantity * avgBuyPrice);
                totalRealizedPL += pnl * fxRate;
            }
        }
        totalRealizedPL = Math.Round(totalRealizedPL, 2);

        // Cumulative cash flow & fees calculation
        var buyTransactions = transactions.Where(t => t.TransactionType == "BUY").ToList();
        var sellTransactions = transactions.Where(t => t.TransactionType == "SELL").ToList();

        decimal cumulativeBuyCost = 0m;
        decimal totalBuyFees = 0m;
        foreach (var b in buyTransactions)
        {
            var compCurr = b.Company?.Currency ?? "BDT";
            decimal fxRate = 1.0m;
            if (!string.Equals(compCurr, baseCurrency, StringComparison.OrdinalIgnoreCase))
            {
                var r = exchangeRates.FirstOrDefault(x => x.FromCurrency == compCurr && x.ToCurrency == baseCurrency);
                if (r != null) fxRate = r.Rate;
            }
            cumulativeBuyCost += (b.Quantity * b.PricePerShare) * fxRate;
            totalBuyFees += b.Fee * fxRate;
        }
        var totalBuyOutflow = cumulativeBuyCost + totalBuyFees;

        decimal totalSellProceeds = 0m;
        decimal totalSellFees = 0m;
        foreach (var s in sellTransactions)
        {
            var compCurr = s.Company?.Currency ?? "BDT";
            decimal fxRate = 1.0m;
            if (!string.Equals(compCurr, baseCurrency, StringComparison.OrdinalIgnoreCase))
            {
                var r = exchangeRates.FirstOrDefault(x => x.FromCurrency == compCurr && x.ToCurrency == baseCurrency);
                if (r != null) fxRate = r.Rate;
            }
            totalSellProceeds += (s.Quantity * s.PricePerShare) * fxRate;
            totalSellFees += s.Fee * fxRate;
        }
        var totalSellInflow = totalSellProceeds - totalSellFees;

        var totalFees = Math.Round(totalBuyFees + totalSellFees, 2);
        var netCashFlow = Math.Round(totalSellInflow - totalBuyOutflow, 2);

        // Calculate Inflation-Adjusted (Real) Return
        decimal? inflationRate = null;
        decimal? realReturnPct = null;
        string inflationStatus = "No active investments";

        if (totalInvested > 0)
        {
            var startYear = transactions.Any() ? transactions.Min(t => t.TransactionDate).Year : DateTime.UtcNow.Year;
            var endYear = DateTime.UtcNow.Year;
            var country = baseCurrency.Equals("USD", StringComparison.OrdinalIgnoreCase) ? "US" : "BD";

            var realReturnResult = await _inflationService.CalculateRealReturnAsync(plPct, startYear, endYear, country, cancellationToken);
            inflationRate = realReturnResult.InflationRate;
            realReturnPct = realReturnResult.RealReturn;
            inflationStatus = realReturnResult.Status;
        }

        return new PortfolioSummaryDto
        {
            PortfolioId = portfolio.PortfolioId,
            PortfolioName = portfolio.PortfolioName,
            Description = portfolio.Description,
            CreatedAt = portfolio.CreatedAt,
            Currency = baseCurrency,
            TotalValue = Math.Round(totalValue, 2),
            TotalInvested = Math.Round(totalInvested, 2),
            UnrealizedProfitLoss = Math.Round(unrealizedPL, 2),
            UnrealizedProfitLossPercentage = plPct,
            RealizedProfitLoss = totalRealizedPL,
            TotalFees = totalFees,
            NetCashFlow = netCashFlow,
            InflationRate = inflationRate,
            RealReturnPercentage = realReturnPct,
            InflationStatus = inflationStatus,
            HoldingsCount = holdings.Count,
            TransactionsCount = transactions.Count
        };
    }
}
