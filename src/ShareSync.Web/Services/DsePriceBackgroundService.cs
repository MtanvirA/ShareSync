using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Services;

public class DsePriceBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DsePriceBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    public DsePriceBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DsePriceBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DSE Live Price Background Worker initialized (Interval: {Interval} mins).", Interval.TotalMinutes);

        // Initial delay after server boot before the first sync
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // Initial price check on startup
        await RunSyncCycleAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSyncCycleAsync(stoppingToken);
        }
    }

    private async Task RunSyncCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting scheduled DSE market price synchronization cycle...");
            using var scope = _scopeFactory.CreateScope();
            var priceService = scope.ServiceProvider.GetRequiredService<IDsePriceService>();

            var response = await priceService.SyncAllCompanyPricesAsync(cancellationToken);
            if (response.Success && response.Data != null)
            {
                _logger.LogInformation(
                    "DSE market price synchronization finished: {UpdatedCount} company prices updated.",
                    response.Data.UpdatedCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred during background DSE price sync: {Message}", ex.Message);
        }
    }
}
