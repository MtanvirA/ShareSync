using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Search;
using ShareSync.Application.Interfaces;

namespace ShareSync.Application.Services;

public class SearchService : ISearchService
{
    private readonly IApplicationDbContext _context;

    public SearchService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<GlobalSearchResultDto>> SearchAsync(
        string? query,
        int userId,
        int limitPerCategory = 5,
        CancellationToken cancellationToken = default)
    {
        var result = new GlobalSearchResultDto
        {
            Query = query?.Trim() ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(query))
        {
            return ApiResponse<GlobalSearchResultDto>.Ok(result, "Empty search query provided.");
        }

        var term = query.Trim();
        var limit = Math.Clamp(limitPerCategory, 1, 20);

        // 1. Search Companies (by TickerSymbol or CompanyName)
        var companies = await _context.Companies
            .AsNoTracking()
            .Include(c => c.Sector)
            .Where(c => c.TickerSymbol.ToLower().Contains(term.ToLower()) ||
                        c.CompanyName.ToLower().Contains(term.ToLower()))
            .OrderBy(c => c.TickerSymbol.ToLower().StartsWith(term.ToLower()) ? 0 : 1)
            .ThenBy(c => c.TickerSymbol)
            .Take(limit)
            .Select(c => new SearchResultItemDto
            {
                Id = c.CompanyId,
                Title = $"{c.TickerSymbol} - {c.CompanyName}",
                Subtitle = $"{(c.Sector != null ? c.Sector.SectorName : "Market")} • ৳{c.CurrentPrice:N2}",
                Category = "Companies",
                Url = $"company.html?id={c.CompanyId}",
                Badge = $"৳{c.CurrentPrice:N2}"
            })
            .ToListAsync(cancellationToken);

        result.Companies = companies;

        // 2. Search Sectors
        var sectors = await _context.Sectors
            .AsNoTracking()
            .Where(s => s.SectorName.ToLower().Contains(term.ToLower()))
            .OrderBy(s => s.SectorName.ToLower().StartsWith(term.ToLower()) ? 0 : 1)
            .ThenBy(s => s.SectorName)
            .Take(limit)
            .Select(s => new SearchResultItemDto
            {
                Id = s.SectorId,
                Title = s.SectorName,
                Subtitle = s.Description ?? "Market Sector",
                Category = "Sectors",
                Url = $"analytics.html?sector={Uri.EscapeDataString(s.SectorName)}",
                Badge = "Sector"
            })
            .ToListAsync(cancellationToken);

        result.Sectors = sectors;

        // 3. Search User Portfolios (strictly scoped to authenticated user)
        var portfolios = await _context.Portfolios
            .AsNoTracking()
            .Where(p => p.UserId == userId &&
                       (p.PortfolioName.ToLower().Contains(term.ToLower()) ||
                        (p.Description != null && p.Description.ToLower().Contains(term.ToLower()))))
            .OrderBy(p => p.PortfolioName)
            .Take(limit)
            .Select(p => new SearchResultItemDto
            {
                Id = p.PortfolioId,
                Title = p.PortfolioName,
                Subtitle = p.Description ?? "User Portfolio",
                Category = "Portfolios",
                Url = $"portfolio.html?id={p.PortfolioId}",
                Badge = "Portfolio"
            })
            .ToListAsync(cancellationToken);

        result.Portfolios = portfolios;

        // 4. Search Watchlist Items (strictly scoped to authenticated user)
        var watchlistItems = await _context.WatchlistItems
            .AsNoTracking()
            .Include(w => w.Watchlist)
            .Include(w => w.Company)
            .Where(w => w.Watchlist != null &&
                        w.Watchlist.UserId == userId &&
                        w.Company != null &&
                        (w.Company.TickerSymbol.ToLower().Contains(term.ToLower()) ||
                         w.Company.CompanyName.ToLower().Contains(term.ToLower()) ||
                         w.Watchlist.WatchlistName.ToLower().Contains(term.ToLower())))
            .OrderBy(w => w.Company!.TickerSymbol)
            .Take(limit)
            .Select(w => new SearchResultItemDto
            {
                Id = w.CompanyId,
                Title = $"{w.Company!.TickerSymbol} ({w.Watchlist!.WatchlistName})",
                Subtitle = w.TargetPrice.HasValue
                    ? $"Target: ৳{w.TargetPrice.Value:N2} • {w.Company.CompanyName}"
                    : w.Company.CompanyName,
                Category = "Watchlist",
                Url = $"watchlist.html?watchlistId={w.WatchlistId}&companyId={w.CompanyId}",
                Badge = w.TargetPrice.HasValue ? $"Target: ৳{w.TargetPrice.Value:N2}" : "Watchlist"
            })
            .ToListAsync(cancellationToken);

        result.Watchlist = watchlistItems;

        result.TotalCount = result.Companies.Count + result.Sectors.Count + result.Portfolios.Count + result.Watchlist.Count;

        return ApiResponse<GlobalSearchResultDto>.Ok(result, $"Found {result.TotalCount} matching results.");
    }
}
