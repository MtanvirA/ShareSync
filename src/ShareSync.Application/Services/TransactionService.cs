using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Transactions;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Services;

public class TransactionService : ITransactionService
{
    private readonly IApplicationDbContext _context;

    public TransactionService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<PagedTransactionsDto>> GetPagedTransactionsAsync(
        int userId,
        TransactionFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new TransactionFilterDto();

        if (filter.StartDate.HasValue && filter.EndDate.HasValue && filter.StartDate.Value > filter.EndDate.Value)
        {
            throw new AppException("Start date cannot be after end date.", 400);
        }

        // Enforce user isolation: only transactions from portfolios owned by userId
        var query = _context.Transactions
            .AsNoTracking()
            .Include(t => t.Portfolio)
            .Include(t => t.Company)
            .Where(t => t.Portfolio.UserId == userId);

        if (filter.PortfolioId.HasValue && filter.PortfolioId > 0)
        {
            var portfolio = await _context.Portfolios
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PortfolioId == filter.PortfolioId.Value, cancellationToken);

            if (portfolio != null && portfolio.UserId != userId)
            {
                throw new AppException("You do not have permission to access this portfolio.", 403);
            }

            if (portfolio == null)
            {
                throw new NotFoundException($"Portfolio with ID {filter.PortfolioId.Value} was not found.");
            }

            query = query.Where(t => t.PortfolioId == filter.PortfolioId.Value);
        }

        if (filter.CompanyId.HasValue && filter.CompanyId > 0)
        {
            query = query.Where(t => t.CompanyId == filter.CompanyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.TransactionType) && filter.TransactionType != "all")
        {
            var normType = filter.TransactionType.Trim().ToUpperInvariant();
            query = query.Where(t => t.TransactionType == normType);
        }

        if (filter.StartDate.HasValue)
        {
            query = query.Where(t => t.TransactionDate >= filter.StartDate.Value);
        }

        if (filter.EndDate.HasValue)
        {
            query = query.Where(t => t.TransactionDate <= filter.EndDate.Value);
        }

        if (filter.MinPrice.HasValue)
        {
            query = query.Where(t => t.PricePerShare >= filter.MinPrice.Value);
        }

        if (filter.MaxPrice.HasValue)
        {
            query = query.Where(t => t.PricePerShare <= filter.MaxPrice.Value);
        }

        if (filter.MinQuantity.HasValue)
        {
            query = query.Where(t => t.Quantity >= filter.MinQuantity.Value);
        }

        if (filter.MaxQuantity.HasValue)
        {
            query = query.Where(t => t.Quantity <= filter.MaxQuantity.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);

        // Sorting
        var sortBy = (filter.SortBy ?? "date").Trim().ToLowerInvariant();
        var isAsc = (filter.SortDirection ?? "desc").Trim().ToLowerInvariant() == "asc";

        query = sortBy switch
        {
            "quantity" or "qty" => isAsc
                ? query.OrderBy(t => t.Quantity).ThenBy(t => t.TransactionDate).ThenBy(t => t.TransactionId)
                : query.OrderByDescending(t => t.Quantity).ThenByDescending(t => t.TransactionDate).ThenByDescending(t => t.TransactionId),

            "price" or "pricepershare" => isAsc
                ? query.OrderBy(t => t.PricePerShare).ThenBy(t => t.TransactionDate).ThenBy(t => t.TransactionId)
                : query.OrderByDescending(t => t.PricePerShare).ThenByDescending(t => t.TransactionDate).ThenByDescending(t => t.TransactionId),

            "value" or "total" or "amount" or "transactionvalue" => isAsc
                ? query.OrderBy(t => t.Quantity * t.PricePerShare).ThenBy(t => t.TransactionDate).ThenBy(t => t.TransactionId)
                : query.OrderByDescending(t => t.Quantity * t.PricePerShare).ThenByDescending(t => t.TransactionDate).ThenByDescending(t => t.TransactionId),

            _ => isAsc
                ? query.OrderBy(t => t.TransactionDate).ThenBy(t => t.TransactionId)
                : query.OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.TransactionId)
        };

        // Pagination
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : (filter.PageSize > 100 ? 100 : filter.PageSize);
        var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalItems / pageSize) : 0;
        var skip = (page - 1) * pageSize;

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .Select(t => new TransactionDto
            {
                TransactionId = t.TransactionId,
                PortfolioId = t.PortfolioId,
                PortfolioName = t.Portfolio.PortfolioName,
                CompanyId = t.CompanyId,
                CompanyName = t.Company.CompanyName,
                TickerSymbol = t.Company.TickerSymbol,
                TransactionType = t.TransactionType,
                Quantity = t.Quantity,
                PricePerShare = t.PricePerShare,
                TotalAmount = Math.Round(t.Quantity * t.PricePerShare, 2),
                TransactionDate = t.TransactionDate
            })
            .ToListAsync(cancellationToken);

        var pagedResult = new PagedTransactionsDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };

        return ApiResponse<PagedTransactionsDto>.Ok(pagedResult);
    }

    public async Task<ApiResponse<List<TransactionDto>>> GetUserTransactionsAsync(
        int userId,
        TransactionFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var unpagedFilter = filter == null
            ? new TransactionFilterDto { Page = 1, PageSize = int.MaxValue }
            : new TransactionFilterDto
            {
                PortfolioId = filter.PortfolioId,
                CompanyId = filter.CompanyId,
                TransactionType = filter.TransactionType,
                StartDate = filter.StartDate,
                EndDate = filter.EndDate,
                MinPrice = filter.MinPrice,
                MaxPrice = filter.MaxPrice,
                MinQuantity = filter.MinQuantity,
                MaxQuantity = filter.MaxQuantity,
                SortBy = filter.SortBy,
                SortDirection = filter.SortDirection,
                Page = 1,
                PageSize = int.MaxValue
            };

        var paged = await GetPagedTransactionsAsync(userId, unpagedFilter, cancellationToken);
        return ApiResponse<List<TransactionDto>>.Ok(paged.Data?.Items ?? new List<TransactionDto>());
    }

    public async Task<ApiResponse<TransactionDto>> GetTransactionByIdAsync(
        int transactionId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Portfolio)
            .Include(t => t.Company)
            .FirstOrDefaultAsync(t => t.TransactionId == transactionId, cancellationToken);

        if (transaction == null)
        {
            throw new NotFoundException("Transaction", transactionId);
        }

        // Strict ownership check
        if (transaction.Portfolio.UserId != userId)
        {
            throw new AppException("You do not have permission to view this transaction.", 403);
        }

        var dto = MapToDto(transaction, transaction.Portfolio.PortfolioName, transaction.Company);
        return ApiResponse<TransactionDto>.Ok(dto);
    }

    public async Task<ApiResponse<decimal>> GetAvailableQuantityAsync(
        int portfolioId,
        int companyId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var portfolio = await _context.Portfolios
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PortfolioId == portfolioId, cancellationToken);

        if (portfolio == null)
        {
            throw new NotFoundException("Portfolio", portfolioId);
        }

        if (portfolio.UserId != userId)
        {
            throw new AppException("You do not have permission to access this portfolio.", 403);
        }

        var transactions = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.PortfolioId == portfolioId && t.CompanyId == companyId)
            .ToListAsync(cancellationToken);

        var totalBought = transactions.Where(t => t.TransactionType == "BUY").Sum(t => t.Quantity);
        var totalSold = transactions.Where(t => t.TransactionType == "SELL").Sum(t => t.Quantity);
        var available = totalBought - totalSold;

        return ApiResponse<decimal>.Ok(available > 0 ? available : 0);
    }

    public async Task<ApiResponse<TransactionDto>> CreateTransactionAsync(
        CreateTransactionRequestDto request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate authenticated user
        if (userId <= 0)
        {
            throw new UnauthorizedException("User session is invalid.");
        }

        // 2. Validate portfolio ownership
        var portfolio = await _context.Portfolios
            .FirstOrDefaultAsync(p => p.PortfolioId == request.PortfolioId, cancellationToken);

        if (portfolio == null)
        {
            throw new NotFoundException("Portfolio", request.PortfolioId);
        }

        if (portfolio.UserId != userId)
        {
            throw new AppException("You do not have permission to execute transactions in this portfolio.", 403);
        }

        // 3. Validate company
        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.CompanyId == request.CompanyId, cancellationToken);

        if (company == null)
        {
            throw new NotFoundException("Company", request.CompanyId);
        }

        // 4. Validate transaction type
        var type = request.TransactionType?.Trim().ToUpperInvariant();
        if (type != "BUY" && type != "SELL")
        {
            throw new AppException("Transaction type must be either 'BUY' or 'SELL'.", 400);
        }

        // 5. Validate quantity
        if (request.Quantity <= 0)
        {
            throw new AppException("Quantity must be greater than zero.", 400);
        }

        if (request.Quantity != Math.Floor(request.Quantity))
        {
            throw new AppException("Fractional shares are not supported. Quantity must be a whole integer.", 400);
        }

        // 6. Validate price
        if (request.PricePerShare <= 0)
        {
            throw new AppException("Price per share must be greater than zero.", 400);
        }

        // Date validation
        var txDate = request.TransactionDate ?? DateTime.UtcNow;
        if (txDate.Date > DateTime.UtcNow.Date.AddDays(1))
        {
            throw new AppException("Transaction date cannot be in the future.", 400);
        }

        // 7 & 8. For SELL, calculate current available quantity and reject if oversell
        var existingTransactions = await _context.Transactions
            .Where(t => t.PortfolioId == request.PortfolioId && t.CompanyId == request.CompanyId)
            .ToListAsync(cancellationToken);

        var totalBought = existingTransactions.Where(t => t.TransactionType == "BUY").Sum(t => t.Quantity);
        var totalSold = existingTransactions.Where(t => t.TransactionType == "SELL").Sum(t => t.Quantity);
        var availableQuantity = totalBought - totalSold;

        if (type == "SELL")
        {
            if (request.Quantity > availableQuantity)
            {
                throw new BusinessRuleException(
                    $"Cannot execute SELL order: Requested quantity ({request.Quantity:N2}) exceeds available holdings ({availableQuantity:N2}) of {company.TickerSymbol}."
                );
            }
        }

        // Atomic transaction execution (Steps 9, 10, 11, 12)
        await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // 9. Insert the transaction
            var transaction = new Transaction
            {
                PortfolioId = request.PortfolioId,
                CompanyId = request.CompanyId,
                TransactionType = type,
                Quantity = request.Quantity,
                PricePerShare = request.PricePerShare,
                TransactionDate = txDate
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync(cancellationToken);

            // 10. Ensure transaction auditing occurs
            var audit = new TransactionAudit
            {
                TransactionId = transaction.TransactionId,
                ActionType = "INSERT",
                ActionDate = DateTime.UtcNow,
                ChangedBy = $"User #{userId}",
                Details = $"{type} {request.Quantity:N4} shares of {company.TickerSymbol} @ ৳{request.PricePerShare:N2}. Total: ৳{request.Quantity * request.PricePerShare:N2}. {request.Notes}".Trim()
            };

            _context.TransactionAudits.Add(audit);
            await _context.SaveChangesAsync(cancellationToken);

            // 11. Commit only if every operation succeeds
            await dbTransaction.CommitAsync(cancellationToken);

            var dto = MapToDto(transaction, portfolio.PortfolioName, company);
            return ApiResponse<TransactionDto>.Ok(dto, $"{type} transaction recorded successfully.");
        }
        catch (Exception)
        {
            // 12. Roll back everything if any step fails
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ApiResponse<TransactionDto>> UpdateTransactionAsync(
        int transactionId,
        UpdateTransactionRequestDto request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Transactions
            .Include(t => t.Portfolio)
            .Include(t => t.Company)
            .FirstOrDefaultAsync(t => t.TransactionId == transactionId, cancellationToken);

        if (transaction == null)
        {
            throw new NotFoundException("Transaction", transactionId);
        }

        // Strict ownership check
        if (transaction.Portfolio.UserId != userId)
        {
            throw new AppException("You do not have permission to modify this transaction.", 403);
        }

        if (request.Quantity <= 0)
        {
            throw new AppException("Quantity must be greater than zero.", 400);
        }

        if (request.Quantity != Math.Floor(request.Quantity))
        {
            throw new AppException("Fractional shares are not supported. Quantity must be a whole integer.", 400);
        }

        if (request.PricePerShare <= 0)
        {
            throw new AppException("Price per share must be greater than zero.", 400);
        }

        if (request.TransactionDate.HasValue && request.TransactionDate.Value.Date > DateTime.UtcNow.Date.AddDays(1))
        {
            throw new AppException("Transaction date cannot be in the future.", 400);
        }

        // Financial integrity check: verify new quantity does not cause negative holdings
        var otherTxs = await _context.Transactions
            .Where(t => t.PortfolioId == transaction.PortfolioId && t.CompanyId == transaction.CompanyId && t.TransactionId != transactionId)
            .ToListAsync(cancellationToken);

        var otherBuys = otherTxs.Where(t => t.TransactionType == "BUY").Sum(t => t.Quantity);
        var otherSells = otherTxs.Where(t => t.TransactionType == "SELL").Sum(t => t.Quantity);

        decimal newTotalBuys = otherBuys + (transaction.TransactionType == "BUY" ? request.Quantity : 0);
        decimal newTotalSells = otherSells + (transaction.TransactionType == "SELL" ? request.Quantity : 0);

        if (newTotalSells > newTotalBuys)
        {
            throw new BusinessRuleException(
                $"Cannot update transaction: New quantity would result in negative holdings (Total Bought: {newTotalBuys:N2}, Total Sold: {newTotalSells:N2}) for {transaction.Company.TickerSymbol}."
            );
        }

        await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var oldQty = transaction.Quantity;
            var oldPrice = transaction.PricePerShare;

            transaction.Quantity = request.Quantity;
            transaction.PricePerShare = request.PricePerShare;
            if (request.TransactionDate.HasValue)
            {
                transaction.TransactionDate = request.TransactionDate.Value;
            }

            await _context.SaveChangesAsync(cancellationToken);

            var audit = new TransactionAudit
            {
                TransactionId = transaction.TransactionId,
                ActionType = "UPDATE",
                ActionDate = DateTime.UtcNow,
                ChangedBy = $"User #{userId}",
                Details = $"Updated {transaction.TransactionType} {transaction.Company.TickerSymbol}: Qty {oldQty:N4} -> {request.Quantity:N4}, Price ৳{oldPrice:N2} -> ৳{request.PricePerShare:N2}. {request.Notes}".Trim()
            };

            _context.TransactionAudits.Add(audit);
            await _context.SaveChangesAsync(cancellationToken);

            await dbTransaction.CommitAsync(cancellationToken);

            var dto = MapToDto(transaction, transaction.Portfolio.PortfolioName, transaction.Company);
            return ApiResponse<TransactionDto>.Ok(dto, "Transaction updated successfully.");
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ApiResponse> DeleteTransactionAsync(
        int transactionId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Transactions
            .Include(t => t.Portfolio)
            .Include(t => t.Company)
            .FirstOrDefaultAsync(t => t.TransactionId == transactionId, cancellationToken);

        if (transaction == null)
        {
            throw new NotFoundException("Transaction", transactionId);
        }

        // Strict ownership check
        if (transaction.Portfolio.UserId != userId)
        {
            throw new AppException("You do not have permission to delete this transaction.", 403);
        }

        // Financial integrity check: Deleting a BUY must not cause subsequent holdings to be negative
        if (transaction.TransactionType == "BUY")
        {
            var allTxs = await _context.Transactions
                .Where(t => t.PortfolioId == transaction.PortfolioId && t.CompanyId == transaction.CompanyId)
                .ToListAsync(cancellationToken);

            var totalBuys = allTxs.Where(t => t.TransactionType == "BUY").Sum(t => t.Quantity);
            var totalSells = allTxs.Where(t => t.TransactionType == "SELL").Sum(t => t.Quantity);

            var remainingBuys = totalBuys - transaction.Quantity;
            if (remainingBuys < totalSells)
            {
                throw new BusinessRuleException(
                    $"Cannot delete this BUY transaction: Existing SELL orders require these shares. Remaining shares would be {remainingBuys - totalSells:N2} for {transaction.Company.TickerSymbol}."
                );
            }
        }

        await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // 1. Audit the deletion before removing row
            var audit = new TransactionAudit
            {
                TransactionId = null, // Detached so audit record is retained permanently
                ActionType = "DELETE",
                ActionDate = DateTime.UtcNow,
                ChangedBy = $"User #{userId}",
                Details = $"Deleted transaction #{transaction.TransactionId}: {transaction.TransactionType} {transaction.Quantity:N4} shares of {transaction.Company.TickerSymbol} @ ৳{transaction.PricePerShare:N2} (Portfolio: {transaction.Portfolio.PortfolioName})"
            };
            _context.TransactionAudits.Add(audit);

            // 2. Detach existing audit records from this transaction so foreign key constraint does not block
            var existingAudits = await _context.TransactionAudits
                .Where(a => a.TransactionId == transactionId)
                .ToListAsync(cancellationToken);

            foreach (var a in existingAudits)
            {
                a.TransactionId = null;
            }

            // 3. Remove transaction
            _context.Transactions.Remove(transaction);
            await _context.SaveChangesAsync(cancellationToken);

            await dbTransaction.CommitAsync(cancellationToken);

            return ApiResponse.Ok("Transaction deleted successfully.");
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static TransactionDto MapToDto(Transaction t, string portfolioName, Company company)
    {
        return new TransactionDto
        {
            TransactionId = t.TransactionId,
            PortfolioId = t.PortfolioId,
            PortfolioName = portfolioName,
            CompanyId = t.CompanyId,
            CompanyName = company.CompanyName,
            TickerSymbol = company.TickerSymbol,
            TransactionType = t.TransactionType,
            Quantity = t.Quantity,
            PricePerShare = t.PricePerShare,
            TotalAmount = Math.Round(t.Quantity * t.PricePerShare, 2),
            TransactionDate = t.TransactionDate
        };
    }
}
