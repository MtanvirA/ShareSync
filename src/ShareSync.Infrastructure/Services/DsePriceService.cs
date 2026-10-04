using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.Interfaces;

namespace ShareSync.Infrastructure.Services;

public class DsePriceService : IDsePriceService
{
    private readonly IApplicationDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly ILogger<DsePriceService> _logger;

    public DsePriceService(
        IApplicationDbContext context,
        HttpClient httpClient,
        ILogger<DsePriceService> logger)
    {
        _context = context;
        _httpClient = httpClient;
        _logger = logger;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ShareSync-PortfolioTracker/1.0 (+https://github.com/MtanvirA/ShareSync)");
        }
    }

    public async Task<decimal?> FetchLatestPriceAsync(string tickerSymbol, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tickerSymbol))
        {
            return null;
        }

        var normalizedTicker = tickerSymbol.Trim().ToUpperInvariant();
        var url = $"https://stockchartbd.com/api/v1.php?endpoint=quote&symbol={Uri.EscapeDataString(normalizedTicker)}";

        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("DSE price fetch for {Ticker} returned HTTP {StatusCode}", normalizedTicker, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("ok", out var okEl) && okEl.GetBoolean() &&
                root.TryGetProperty("quote", out var quoteEl))
            {
                if (quoteEl.TryGetProperty("close", out var closeEl) && closeEl.TryGetDecimal(out var closePrice) && closePrice > 0)
                {
                    return closePrice;
                }

                if (quoteEl.TryGetProperty("open", out var openEl) && openEl.TryGetDecimal(out var openPrice) && openPrice > 0)
                {
                    return openPrice;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve live DSE quote for ticker {Ticker}: {Message}", normalizedTicker, ex.Message);
        }

        return null;
    }

    public async Task<ApiResponse<DsePriceUpdateResultDto>> SyncAllCompanyPricesAsync(CancellationToken cancellationToken = default)
    {
        var companies = await _context.Companies.ToListAsync(cancellationToken);
        var result = new DsePriceUpdateResultDto
        {
            SyncedAt = DateTime.UtcNow
        };

        if (companies.Count == 0)
        {
            return ApiResponse<DsePriceUpdateResultDto>.Ok(result, "No companies found in database to update.");
        }

        int updatedCount = 0;
        foreach (var company in companies)
        {
            var newPrice = await FetchLatestPriceAsync(company.TickerSymbol, cancellationToken);
            if (newPrice.HasValue && newPrice.Value > 0)
            {
                if (company.CurrentPrice != newPrice.Value)
                {
                    _logger.LogInformation(
                        "Updating DSE price for {Ticker}: {OldPrice:C} -> {NewPrice:C}",
                        company.TickerSymbol, company.CurrentPrice, newPrice.Value);

                    company.CurrentPrice = newPrice.Value;
                    updatedCount++;
                }

                result.UpdatedPrices[company.TickerSymbol] = newPrice.Value;
            }
            else
            {
                result.UpdatedPrices[company.TickerSymbol] = company.CurrentPrice;
            }
        }

        if (updatedCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        result.UpdatedCount = updatedCount;
        return ApiResponse<DsePriceUpdateResultDto>.Ok(
            result,
            $"Successfully synchronized DSE prices for {companies.Count} companies ({updatedCount} updated).");
    }
}
