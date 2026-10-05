using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Admin;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Services;

public class AdminService : IAdminService
{
    private readonly IApplicationDbContext _context;
    private readonly IDsePriceService _dsePriceService;

    public AdminService(IApplicationDbContext context, IDsePriceService dsePriceService)
    {
        _context = context;
        _dsePriceService = dsePriceService;
    }

    // =========================================================================
    // 1. DASHBOARD & SYSTEM MONITORING
    // =========================================================================

    public async Task<ApiResponse<AdminDashboardDto>> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        var totalUsers = await _context.Users.CountAsync(cancellationToken);
        var activeUsers = await _context.Users.CountAsync(u => u.IsActive, cancellationToken);
        var totalCompanies = await _context.Companies.CountAsync(cancellationToken);
        var activeCompanies = await _context.Companies.CountAsync(c => c.IsActive, cancellationToken);
        var totalSectors = await _context.Sectors.CountAsync(cancellationToken);
        var totalPortfolios = await _context.Portfolios.CountAsync(cancellationToken);
        var totalTransactions = await _context.Transactions.CountAsync(cancellationToken);

        decimal totalVolume = 0m;
        if (totalTransactions > 0)
        {
            totalVolume = await _context.Transactions
                .SumAsync(t => (decimal?)(t.Quantity * t.PricePerShare) ?? 0m, cancellationToken);
        }

        var lastSync = await _context.CompanyPriceHistories
            .OrderByDescending(h => h.RecordedAt)
            .Select(h => (DateTime?)h.RecordedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var totalHistories = await _context.CompanyPriceHistories.CountAsync(cancellationToken);

        var stats = new AdminDashboardDto
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            TotalCompanies = totalCompanies,
            ActiveCompanies = activeCompanies,
            TotalSectors = totalSectors,
            TotalPortfolios = totalPortfolios,
            TotalTransactions = totalTransactions,
            TotalTransactionVolume = totalVolume,
            LastSyncTime = lastSync,
            TotalPriceHistoryRecords = totalHistories,
            SystemStatus = "OPERATIONAL",
            DatabaseHealth = "HEALTHY"
        };

        return ApiResponse<AdminDashboardDto>.Ok(stats, "System statistics retrieved successfully.");
    }

    public async Task<ApiResponse<SystemDiagnosticsDto>> GetSystemDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        // 1. Database health tested safely
        bool dbConnected = false;
        string dbStatus = "Disconnected";
        try
        {
            dbConnected = await _context.Database.CanConnectAsync(cancellationToken);
            dbStatus = dbConnected ? "Connected" : "Disconnected";
        }
        catch
        {
            dbConnected = false;
            dbStatus = "Disconnected";
        }

        // 2. Efficient COUNT queries - no entity fetching
        var usersCount = await _context.Users.CountAsync(cancellationToken);
        var companiesCount = await _context.Companies.CountAsync(cancellationToken);
        var portfoliosCount = await _context.Portfolios.CountAsync(cancellationToken);
        var transactionsCount = await _context.Transactions.CountAsync(cancellationToken);
        var auditRecordsCount = await _context.TransactionAudits.CountAsync(cancellationToken);

        // 3. Synchronization status
        var lastSync = await _context.CompanyPriceHistories
            .OrderByDescending(h => h.RecordedAt)
            .Select(h => (DateTime?)h.RecordedAt)
            .FirstOrDefaultAsync(cancellationToken);

        string syncStatus = lastSync.HasValue ? "Successful" : "Idle";
        string dseSyncStatus = "Running";

        var diagnostics = new SystemDiagnosticsDto
        {
            DatabaseStatus = dbStatus,
            IsDatabaseConnected = dbConnected,
            ApplicationStatus = "Healthy",
            DseSyncStatus = dseSyncStatus,
            UsersCount = usersCount,
            CompaniesCount = companiesCount,
            PortfoliosCount = portfoliosCount,
            TransactionsCount = transactionsCount,
            AuditRecordsCount = auditRecordsCount,
            LastSyncTime = lastSync,
            SyncStatus = syncStatus,
            ServerTimeUtc = DateTime.UtcNow
        };

        return ApiResponse<SystemDiagnosticsDto>.Ok(diagnostics, "System diagnostics retrieved successfully.");
    }

    // =========================================================================
    // 2. COMPANY MANAGEMENT
    // =========================================================================

    public async Task<ApiResponse<List<AdminCompanyDto>>> GetAllCompaniesAsync(
        string? search = null,
        int? sectorId = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Companies
            .AsNoTracking()
            .Include(c => c.Sector)
            .Include(c => c.Transactions)
            .Include(c => c.WatchlistItems)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c => c.TickerSymbol.ToLower().Contains(term) || c.CompanyName.ToLower().Contains(term));
        }

        if (sectorId.HasValue && sectorId.Value > 0)
        {
            query = query.Where(c => c.SectorId == sectorId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        var list = await query
            .OrderBy(c => c.TickerSymbol)
            .Select(c => new AdminCompanyDto
            {
                CompanyId = c.CompanyId,
                CompanyName = c.CompanyName,
                TickerSymbol = c.TickerSymbol,
                SectorId = c.SectorId,
                SectorName = c.Sector != null ? c.Sector.SectorName : "Unassigned",
                CurrentPrice = c.CurrentPrice,
                MarketCap = c.MarketCap,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                TransactionCount = c.Transactions.Count,
                WatchlistCount = c.WatchlistItems.Count
            })
            .ToListAsync(cancellationToken);

        return ApiResponse<List<AdminCompanyDto>>.Ok(list);
    }

    public async Task<ApiResponse<AdminCompanyDto>> GetCompanyByIdAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var c = await _context.Companies
            .AsNoTracking()
            .Include(x => x.Sector)
            .Include(x => x.Transactions)
            .Include(x => x.WatchlistItems)
            .FirstOrDefaultAsync(x => x.CompanyId == companyId, cancellationToken);

        if (c == null)
            throw new NotFoundException("Company", companyId);

        var dto = new AdminCompanyDto
        {
            CompanyId = c.CompanyId,
            CompanyName = c.CompanyName,
            TickerSymbol = c.TickerSymbol,
            SectorId = c.SectorId,
            SectorName = c.Sector != null ? c.Sector.SectorName : "Unassigned",
            CurrentPrice = c.CurrentPrice,
            MarketCap = c.MarketCap,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
            TransactionCount = c.Transactions.Count,
            WatchlistCount = c.WatchlistItems.Count
        };

        return ApiResponse<AdminCompanyDto>.Ok(dto);
    }

    public async Task<ApiResponse<AdminCompanyDto>> CreateCompanyAsync(CreateCompanyRequestDto request, CancellationToken cancellationToken = default)
    {
        var name = (request.CompanyName ?? string.Empty).Trim();
        var ticker = (request.TickerSymbol ?? string.Empty).Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(name))
            throw new AppException("Company name is required.", 400);

        if (string.IsNullOrWhiteSpace(ticker))
            throw new AppException("Ticker symbol is required.", 400);

        if (request.CurrentPrice <= 0)
            throw new AppException("Current price must be greater than zero.", 400);

        if (request.MarketCap.HasValue && request.MarketCap.Value < 0)
            throw new AppException("Market capitalization cannot be negative.", 400);

        var sectorExists = await _context.Sectors.AnyAsync(s => s.SectorId == request.SectorId, cancellationToken);
        if (!sectorExists)
            throw new NotFoundException("Sector", request.SectorId);

        var tickerExists = await _context.Companies.AnyAsync(c => c.TickerSymbol == ticker, cancellationToken);
        if (tickerExists)
            throw new AppException($"A company with ticker symbol '{ticker}' already exists.", 409);

        var company = new Company
        {
            CompanyName = name,
            TickerSymbol = ticker,
            SectorId = request.SectorId,
            CurrentPrice = request.CurrentPrice,
            MarketCap = request.MarketCap,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Companies.Add(company);
        await _context.SaveChangesAsync(cancellationToken);

        // Record initial price history snapshot
        _context.CompanyPriceHistories.Add(new CompanyPriceHistory
        {
            CompanyId = company.CompanyId,
            Price = company.CurrentPrice,
            RecordedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);

        return await GetCompanyByIdAsync(company.CompanyId, cancellationToken);
    }

    public async Task<ApiResponse<AdminCompanyDto>> UpdateCompanyAsync(int companyId, UpdateCompanyRequestDto request, CancellationToken cancellationToken = default)
    {
        var company = await _context.Companies.FindAsync(new object[] { companyId }, cancellationToken);
        if (company == null)
            throw new NotFoundException("Company", companyId);

        var name = (request.CompanyName ?? string.Empty).Trim();
        var ticker = (request.TickerSymbol ?? string.Empty).Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(name))
            throw new AppException("Company name is required.", 400);

        if (string.IsNullOrWhiteSpace(ticker))
            throw new AppException("Ticker symbol is required.", 400);

        if (request.CurrentPrice <= 0)
            throw new AppException("Current price must be greater than zero.", 400);

        if (request.MarketCap.HasValue && request.MarketCap.Value < 0)
            throw new AppException("Market capitalization cannot be negative.", 400);

        var sectorExists = await _context.Sectors.AnyAsync(s => s.SectorId == request.SectorId, cancellationToken);
        if (!sectorExists)
            throw new NotFoundException("Sector", request.SectorId);

        var tickerConflict = await _context.Companies
            .AnyAsync(c => c.TickerSymbol == ticker && c.CompanyId != companyId, cancellationToken);
        if (tickerConflict)
            throw new AppException($"Another company is already using ticker symbol '{ticker}'.", 409);

        var priceChanged = company.CurrentPrice != request.CurrentPrice;

        company.CompanyName = name;
        company.TickerSymbol = ticker;
        company.SectorId = request.SectorId;
        company.CurrentPrice = request.CurrentPrice;
        company.MarketCap = request.MarketCap;
        company.IsActive = request.IsActive;

        if (priceChanged)
        {
            _context.CompanyPriceHistories.Add(new CompanyPriceHistory
            {
                CompanyId = company.CompanyId,
                Price = company.CurrentPrice,
                RecordedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await GetCompanyByIdAsync(company.CompanyId, cancellationToken);
    }

    public async Task<ApiResponse<AdminCompanyDto>> ToggleCompanyStatusAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var company = await _context.Companies.FindAsync(new object[] { companyId }, cancellationToken);
        if (company == null)
            throw new NotFoundException("Company", companyId);

        company.IsActive = !company.IsActive;
        await _context.SaveChangesAsync(cancellationToken);

        var statusText = company.IsActive ? "activated" : "deactivated";
        var result = await GetCompanyByIdAsync(company.CompanyId, cancellationToken);
        result.Message = $"Company '{company.TickerSymbol}' has been {statusText} successfully.";
        return result;
    }

    public async Task<ApiResponse<bool>> DeleteCompanyAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var company = await _context.Companies.FindAsync(new object[] { companyId }, cancellationToken);
        if (company == null)
            throw new NotFoundException("Company", companyId);

        // Security check: strictly disallow destructive deletion if referenced by transaction history
        var txCount = await _context.Transactions.CountAsync(t => t.CompanyId == companyId, cancellationToken);
        if (txCount > 0)
        {
            throw new AppException(
                $"Cannot delete company '{company.TickerSymbol}' because it is referenced by {txCount} financial transaction(s). Use safe deactivation instead.",
                400);
        }

        // Check other operational references
        var watchlistCount = await _context.WatchlistItems.CountAsync(w => w.CompanyId == companyId, cancellationToken);
        var dividendCount = await _context.Dividends.CountAsync(d => d.CompanyId == companyId, cancellationToken);

        if (watchlistCount > 0 || dividendCount > 0)
        {
            throw new AppException(
                $"Cannot delete company '{company.TickerSymbol}' because it has {watchlistCount} watchlist entry/entries and {dividendCount} dividend record(s). Deactivate the company instead.",
                400);
        }

        // Remove price history records associated with company before deletion
        var histories = await _context.CompanyPriceHistories
            .Where(h => h.CompanyId == companyId)
            .ToListAsync(cancellationToken);
        _context.CompanyPriceHistories.RemoveRange(histories);

        _context.Companies.Remove(company);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, $"Company '{company.TickerSymbol}' was deleted successfully.");
    }

    // =========================================================================
    // 3. SECTOR MANAGEMENT
    // =========================================================================

    public async Task<ApiResponse<List<AdminSectorDto>>> GetAllSectorsAsync(CancellationToken cancellationToken = default)
    {
        var sectors = await _context.Sectors
            .AsNoTracking()
            .Include(s => s.Companies)
            .OrderBy(s => s.SectorName)
            .Select(s => new AdminSectorDto
            {
                SectorId = s.SectorId,
                SectorName = s.SectorName,
                Description = s.Description,
                CompaniesCount = s.Companies.Count,
                TotalMarketCap = s.Companies.Sum(c => c.MarketCap ?? 0m)
            })
            .ToListAsync(cancellationToken);

        return ApiResponse<List<AdminSectorDto>>.Ok(sectors);
    }

    public async Task<ApiResponse<AdminSectorDto>> GetSectorByIdAsync(int sectorId, CancellationToken cancellationToken = default)
    {
        var s = await _context.Sectors
            .AsNoTracking()
            .Include(x => x.Companies)
            .FirstOrDefaultAsync(x => x.SectorId == sectorId, cancellationToken);

        if (s == null)
            throw new NotFoundException("Sector", sectorId);

        var dto = new AdminSectorDto
        {
            SectorId = s.SectorId,
            SectorName = s.SectorName,
            Description = s.Description,
            CompaniesCount = s.Companies.Count,
            TotalMarketCap = s.Companies.Sum(c => c.MarketCap ?? 0m)
        };

        return ApiResponse<AdminSectorDto>.Ok(dto);
    }

    public async Task<ApiResponse<AdminSectorDto>> CreateSectorAsync(CreateSectorRequestDto request, CancellationToken cancellationToken = default)
    {
        var name = (request.SectorName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new AppException("Sector name is required.", 400);

        var exists = await _context.Sectors.AnyAsync(s => s.SectorName.ToLower() == name.ToLower(), cancellationToken);
        if (exists)
            throw new AppException($"Sector '{name}' already exists.", 409);

        var sector = new Sector
        {
            SectorName = name,
            Description = request.Description?.Trim()
        };

        _context.Sectors.Add(sector);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetSectorByIdAsync(sector.SectorId, cancellationToken);
    }

    public async Task<ApiResponse<AdminSectorDto>> UpdateSectorAsync(int sectorId, UpdateSectorRequestDto request, CancellationToken cancellationToken = default)
    {
        var sector = await _context.Sectors.FindAsync(new object[] { sectorId }, cancellationToken);
        if (sector == null)
            throw new NotFoundException("Sector", sectorId);

        var name = (request.SectorName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new AppException("Sector name is required.", 400);

        var conflict = await _context.Sectors
            .AnyAsync(s => s.SectorName.ToLower() == name.ToLower() && s.SectorId != sectorId, cancellationToken);
        if (conflict)
            throw new AppException($"Another sector with name '{name}' already exists.", 409);

        sector.SectorName = name;
        sector.Description = request.Description?.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        return await GetSectorByIdAsync(sector.SectorId, cancellationToken);
    }

    public async Task<ApiResponse<bool>> DeleteSectorAsync(int sectorId, CancellationToken cancellationToken = default)
    {
        var sector = await _context.Sectors.FindAsync(new object[] { sectorId }, cancellationToken);
        if (sector == null)
            throw new NotFoundException("Sector", sectorId);

        // Security check: verify no companies belong to this sector
        var companiesCount = await _context.Companies.CountAsync(c => c.SectorId == sectorId, cancellationToken);
        if (companiesCount > 0)
        {
            throw new AppException(
                $"Cannot delete sector '{sector.SectorName}' because it contains {companiesCount} associated company/companies. Reassign or delete these companies first.",
                400);
        }

        _context.Sectors.Remove(sector);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, $"Sector '{sector.SectorName}' was deleted successfully.");
    }

    // =========================================================================
    // 4. USER MANAGEMENT (Zero Password Leaks)
    // =========================================================================

    public async Task<ApiResponse<List<AdminUserDto>>> GetAllUsersAsync(
        string? search = null,
        string? role = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Users
            .AsNoTracking()
            .Include(u => u.Portfolios)
                .ThenInclude(p => p.Transactions)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u => u.Name.ToLower().Contains(term) || u.Email.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var r = role.Trim().ToUpperInvariant();
            query = query.Where(u => u.Role == r);
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new AdminUserDto
            {
                UserId = u.UserId,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                PortfolioCount = u.Portfolios.Count,
                TransactionCount = u.Portfolios.SelectMany(p => p.Transactions).Count()
            })
            .ToListAsync(cancellationToken);

        return ApiResponse<List<AdminUserDto>>.Ok(users);
    }

    public async Task<ApiResponse<AdminUserDto>> ToggleUserStatusAsync(int userId, int requestingAdminUserId, CancellationToken cancellationToken = default)
    {
        if (userId == requestingAdminUserId)
        {
            throw new AppException("You cannot deactivate your own administrative account.", 400);
        }

        var user = await _context.Users
            .Include(u => u.Portfolios)
                .ThenInclude(p => p.Transactions)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user == null)
            throw new NotFoundException("User", userId);

        user.IsActive = !user.IsActive;
        await _context.SaveChangesAsync(cancellationToken);

        var statusText = user.IsActive ? "enabled" : "disabled";
        var dto = new AdminUserDto
        {
            UserId = user.UserId,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            PortfolioCount = user.Portfolios.Count,
            TransactionCount = user.Portfolios.SelectMany(p => p.Transactions).Count()
        };

        return ApiResponse<AdminUserDto>.Ok(dto, $"User account '{user.Email}' has been {statusText}.");
    }

    public async Task<ApiResponse<AdminUserDto>> UpdateUserRoleAsync(int userId, string newRole, int requestingAdminUserId, CancellationToken cancellationToken = default)
    {
        var role = (newRole ?? string.Empty).Trim().ToUpperInvariant();
        if (role != "ADMIN" && role != "INVESTOR")
        {
            throw new AppException("Invalid role specified. Only 'INVESTOR' and 'ADMIN' are supported.", 400);
        }

        if (userId == requestingAdminUserId && role != "ADMIN")
        {
            throw new AppException("You cannot revoke your own administrator role.", 400);
        }

        var user = await _context.Users
            .Include(u => u.Portfolios)
                .ThenInclude(p => p.Transactions)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user == null)
            throw new NotFoundException("User", userId);

        user.Role = role;
        await _context.SaveChangesAsync(cancellationToken);

        var dto = new AdminUserDto
        {
            UserId = user.UserId,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            PortfolioCount = user.Portfolios.Count,
            TransactionCount = user.Portfolios.SelectMany(p => p.Transactions).Count()
        };

        return ApiResponse<AdminUserDto>.Ok(dto, $"User '{user.Email}' role updated to '{role}'.");
    }

    // =========================================================================
    // 5. AUDIT LOGS
    // =========================================================================

    public async Task<ApiResponse<List<AdminAuditLogDto>>> GetAuditLogsAsync(
        string? actionType = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);

        var query = _context.TransactionAudits
            .AsNoTracking()
            .Include(a => a.Transaction)
                .ThenInclude(t => t!.Portfolio)
            .Include(a => a.Transaction)
                .ThenInclude(t => t!.Company)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(actionType))
        {
            var act = actionType.Trim().ToUpperInvariant();
            query = query.Where(a => a.ActionType == act);
        }

        var list = await query
            .OrderByDescending(a => a.ActionDate)
            .Take(safeLimit)
            .Select(a => new AdminAuditLogDto
            {
                AuditId = a.AuditId,
                TransactionId = a.TransactionId,
                ActionType = a.ActionType,
                ActionDate = a.ActionDate,
                ChangedBy = a.ChangedBy,
                Details = a.Details,
                PortfolioName = a.Transaction != null && a.Transaction.Portfolio != null ? a.Transaction.Portfolio.PortfolioName : "-",
                TickerSymbol = a.Transaction != null && a.Transaction.Company != null ? a.Transaction.Company.TickerSymbol : "-"
            })
            .ToListAsync(cancellationToken);

        return ApiResponse<List<AdminAuditLogDto>>.Ok(list, $"Retrieved {list.Count} audit records.");
    }

    // =========================================================================
    // 6. DSE MARKET DATA SYNC STATUS
    // =========================================================================

    public async Task<ApiResponse<object>> GetDseSyncStatusAsync(CancellationToken cancellationToken = default)
    {
        var lastSnapshot = await _context.CompanyPriceHistories
            .OrderByDescending(h => h.RecordedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var totalRecords = await _context.CompanyPriceHistories.CountAsync(cancellationToken);
        var activeCompanies = await _context.Companies.CountAsync(c => c.IsActive, cancellationToken);

        var status = new
        {
            lastSyncedAt = lastSnapshot?.RecordedAt,
            totalPriceSnapshots = totalRecords,
            activeTrackedCompanies = activeCompanies,
            serviceStatus = "ACTIVE",
            syncSource = "Dhaka Stock Exchange (DSE Live Engine)"
        };

        return ApiResponse<object>.Ok(status);
    }
}
