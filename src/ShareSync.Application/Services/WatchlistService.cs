using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Watchlists;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Services;

public class WatchlistService : IWatchlistService
{
    private readonly IApplicationDbContext _context;

    public WatchlistService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<WatchlistDto>>> GetUserWatchlistsAsync(int userId, CancellationToken cancellationToken = default)
    {
        var watchlists = await _context.Watchlists
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderBy(w => w.WatchlistName)
            .Select(w => new WatchlistDto
            {
                WatchlistId = w.WatchlistId,
                UserId = w.UserId,
                WatchlistName = w.WatchlistName,
                Description = w.Description,
                CreatedAt = w.CreatedAt,
                ItemCount = w.Items.Count
            })
            .ToListAsync(cancellationToken);

        return ApiResponse<List<WatchlistDto>>.Ok(watchlists);
    }

    public async Task<ApiResponse<WatchlistDetailDto>> GetWatchlistByIdAsync(int watchlistId, int userId, CancellationToken cancellationToken = default)
    {
        var watchlist = await _context.Watchlists
            .AsNoTracking()
            .Include(w => w.Items)
                .ThenInclude(i => i.Company)
            .FirstOrDefaultAsync(w => w.WatchlistId == watchlistId, cancellationToken);

        if (watchlist == null)
        {
            throw new NotFoundException("Watchlist", watchlistId);
        }

        if (watchlist.UserId != userId)
        {
            throw new AppException("You do not have permission to access this watchlist.", 403);
        }

        var items = watchlist.Items
            .Where(i => i.Company != null)
            .OrderBy(i => i.Company!.TickerSymbol)
            .Select(i => MapToWatchlistItemDto(i))
            .ToList();

        var detail = new WatchlistDetailDto
        {
            WatchlistId = watchlist.WatchlistId,
            UserId = watchlist.UserId,
            WatchlistName = watchlist.WatchlistName,
            Description = watchlist.Description,
            CreatedAt = watchlist.CreatedAt,
            Items = items,
            TotalWatching = items.Count,
            AboveTargetCount = items.Count(i => i.IsTargetReached),
            NearTargetCount = items.Count(i => !i.IsTargetReached && i.TargetDistancePercentage.HasValue && i.TargetDistancePercentage.Value <= 5.0m),
            AverageDailyChange = 0
        };

        return ApiResponse<WatchlistDetailDto>.Ok(detail);
    }

    public async Task<ApiResponse<WatchlistDto>> CreateWatchlistAsync(CreateWatchlistRequestDto request, int userId, CancellationToken cancellationToken = default)
    {
        var trimmedName = request.WatchlistName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new AppException("Watchlist name is required.", 400);
        }

        if (trimmedName.Length > 100)
        {
            throw new AppException("Watchlist name cannot exceed 100 characters.", 400);
        }

        var exists = await _context.Watchlists
            .AnyAsync(w => w.UserId == userId && w.WatchlistName.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (exists)
        {
            throw new AppException($"A watchlist named '{trimmedName}' already exists in your account.", 409);
        }

        var watchlist = new Watchlist
        {
            UserId = userId,
            WatchlistName = trimmedName,
            Description = request.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Watchlists.Add(watchlist);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = new WatchlistDto
        {
            WatchlistId = watchlist.WatchlistId,
            UserId = watchlist.UserId,
            WatchlistName = watchlist.WatchlistName,
            Description = watchlist.Description,
            CreatedAt = watchlist.CreatedAt,
            ItemCount = 0
        };

        return ApiResponse<WatchlistDto>.Ok(dto, "Watchlist created successfully.");
    }

    public async Task<ApiResponse<WatchlistDto>> UpdateWatchlistAsync(int watchlistId, UpdateWatchlistRequestDto request, int userId, CancellationToken cancellationToken = default)
    {
        var watchlist = await _context.Watchlists
            .Include(w => w.Items)
            .FirstOrDefaultAsync(w => w.WatchlistId == watchlistId, cancellationToken);

        if (watchlist == null)
        {
            throw new NotFoundException("Watchlist", watchlistId);
        }

        if (watchlist.UserId != userId)
        {
            throw new AppException("You do not have permission to modify this watchlist.", 403);
        }

        var trimmedName = request.WatchlistName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new AppException("Watchlist name is required.", 400);
        }

        if (trimmedName.Length > 100)
        {
            throw new AppException("Watchlist name cannot exceed 100 characters.", 400);
        }

        var exists = await _context.Watchlists
            .AnyAsync(w => w.UserId == userId && w.WatchlistName.ToLower() == trimmedName.ToLower() && w.WatchlistId != watchlistId, cancellationToken);

        if (exists)
        {
            throw new AppException($"A watchlist named '{trimmedName}' already exists in your account.", 409);
        }

        watchlist.WatchlistName = trimmedName;
        watchlist.Description = request.Description?.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        var dto = new WatchlistDto
        {
            WatchlistId = watchlist.WatchlistId,
            UserId = watchlist.UserId,
            WatchlistName = watchlist.WatchlistName,
            Description = watchlist.Description,
            CreatedAt = watchlist.CreatedAt,
            ItemCount = watchlist.Items.Count
        };

        return ApiResponse<WatchlistDto>.Ok(dto, "Watchlist updated successfully.");
    }

    public async Task<ApiResponse> DeleteWatchlistAsync(int watchlistId, int userId, CancellationToken cancellationToken = default)
    {
        var watchlist = await _context.Watchlists
            .Include(w => w.Items)
            .FirstOrDefaultAsync(w => w.WatchlistId == watchlistId, cancellationToken);

        if (watchlist == null)
        {
            throw new NotFoundException("Watchlist", watchlistId);
        }

        if (watchlist.UserId != userId)
        {
            throw new AppException("You do not have permission to delete this watchlist.", 403);
        }

        _context.Watchlists.Remove(watchlist);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse.Ok("Watchlist deleted successfully.");
    }

    public async Task<ApiResponse<WatchlistItemDto>> AddItemAsync(int watchlistId, AddWatchlistItemRequestDto request, int userId, CancellationToken cancellationToken = default)
    {
        var watchlist = await _context.Watchlists
            .FirstOrDefaultAsync(w => w.WatchlistId == watchlistId, cancellationToken);

        if (watchlist == null)
        {
            throw new NotFoundException("Watchlist", watchlistId);
        }

        if (watchlist.UserId != userId)
        {
            throw new AppException("You do not have permission to modify this watchlist.", 403);
        }

        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.CompanyId == request.CompanyId, cancellationToken);

        if (company == null)
        {
            throw new NotFoundException("Company", request.CompanyId);
        }

        if (request.TargetPrice.HasValue && request.TargetPrice.Value <= 0)
        {
            throw new AppException("Target price must be greater than zero.", 400);
        }

        var alreadyExists = await _context.WatchlistItems
            .AnyAsync(wi => wi.WatchlistId == watchlistId && wi.CompanyId == request.CompanyId, cancellationToken);

        if (alreadyExists)
        {
            throw new AppException($"'{company.TickerSymbol}' is already in this watchlist.", 409);
        }

        var item = new WatchlistItem
        {
            WatchlistId = watchlistId,
            CompanyId = request.CompanyId,
            TargetPrice = request.TargetPrice,
            AddedAt = DateTime.UtcNow,
            Company = company
        };

        _context.WatchlistItems.Add(item);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = MapToWatchlistItemDto(item);
        return ApiResponse<WatchlistItemDto>.Ok(dto, $"{company.TickerSymbol} added to watchlist successfully.");
    }

    public async Task<ApiResponse<WatchlistItemDto>> UpdateItemAsync(int watchlistId, int companyId, UpdateWatchlistItemRequestDto request, int userId, CancellationToken cancellationToken = default)
    {
        var watchlist = await _context.Watchlists
            .FirstOrDefaultAsync(w => w.WatchlistId == watchlistId, cancellationToken);

        if (watchlist == null)
        {
            throw new NotFoundException("Watchlist", watchlistId);
        }

        if (watchlist.UserId != userId)
        {
            throw new AppException("You do not have permission to modify this watchlist.", 403);
        }

        var item = await _context.WatchlistItems
            .Include(wi => wi.Company)
            .FirstOrDefaultAsync(wi => wi.WatchlistId == watchlistId && wi.CompanyId == companyId, cancellationToken);

        if (item == null)
        {
            throw new NotFoundException("Watchlist item", $"Watchlist: {watchlistId}, Company: {companyId}");
        }

        if (request.TargetPrice.HasValue && request.TargetPrice.Value <= 0)
        {
            throw new AppException("Target price must be greater than zero.", 400);
        }

        item.TargetPrice = request.TargetPrice;
        await _context.SaveChangesAsync(cancellationToken);

        var dto = MapToWatchlistItemDto(item);
        return ApiResponse<WatchlistItemDto>.Ok(dto, "Target price updated successfully.");
    }

    public async Task<ApiResponse> RemoveItemAsync(int watchlistId, int companyId, int userId, CancellationToken cancellationToken = default)
    {
        var watchlist = await _context.Watchlists
            .FirstOrDefaultAsync(w => w.WatchlistId == watchlistId, cancellationToken);

        if (watchlist == null)
        {
            throw new NotFoundException("Watchlist", watchlistId);
        }

        if (watchlist.UserId != userId)
        {
            throw new AppException("You do not have permission to modify this watchlist.", 403);
        }

        var item = await _context.WatchlistItems
            .FirstOrDefaultAsync(wi => wi.WatchlistId == watchlistId && wi.CompanyId == companyId, cancellationToken);

        if (item == null)
        {
            throw new NotFoundException("Watchlist item", $"Watchlist: {watchlistId}, Company: {companyId}");
        }

        _context.WatchlistItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse.Ok("Company removed from watchlist successfully.");
    }

    private static WatchlistItemDto MapToWatchlistItemDto(WatchlistItem item)
    {
        var currentPrice = item.Company?.CurrentPrice ?? 0;
        var targetPrice = item.TargetPrice;

        bool isTargetReached = false;
        decimal? distancePct = null;

        if (targetPrice.HasValue && targetPrice.Value > 0)
        {
            isTargetReached = currentPrice >= targetPrice.Value;
            if (currentPrice > 0)
            {
                distancePct = Math.Round(((targetPrice.Value - currentPrice) / currentPrice) * 100, 2);
            }
        }

        return new WatchlistItemDto
        {
            WatchlistId = item.WatchlistId,
            CompanyId = item.CompanyId,
            CompanyName = item.Company?.CompanyName ?? string.Empty,
            TickerSymbol = item.Company?.TickerSymbol ?? string.Empty,
            CurrentPrice = currentPrice,
            TargetPrice = targetPrice,
            TargetDistancePercentage = distancePct,
            IsTargetReached = isTargetReached,
            DailyChange = 0,
            DailyChangePercentage = 0,
            AddedAt = item.AddedAt
        };
    }
}
