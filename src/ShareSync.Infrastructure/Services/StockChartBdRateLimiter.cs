using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShareSync.Application.Common.Models;
using ShareSync.Application.Interfaces;

namespace ShareSync.Infrastructure.Services;

/// <summary>
/// Thread-safe rolling 1-hour window request-budget manager for StockChartBD.
/// Prevents exceeding the provider's 120 req/hour limit by enforcing a conservative budget (default 100).
/// Integrates HTTP 429 Retry-After backoff and fair-scheduling queue semantics.
/// FEATURE: DSE Market Data
/// </summary>
public class StockChartBdRateLimiter : IStockChartBdRateLimiter
{
    private readonly int _maxRequestsPerHour;
    private readonly ILogger<StockChartBdRateLimiter> _logger;
    private readonly object _lock = new();
    private readonly Queue<DateTime> _requestTimestamps = new();
    private DateTime? _throttledUntil;

    public StockChartBdRateLimiter(
        IOptions<StockChartBdOptions> options,
        ILogger<StockChartBdRateLimiter> logger)
    {
        _maxRequestsPerHour = options?.Value?.MaxRequestsPerHour is > 0 and <= 120
            ? options.Value.MaxRequestsPerHour
            : 100;
        _logger = logger;
    }

    public bool IsThrottled
    {
        get
        {
            lock (_lock)
            {
                if (_throttledUntil.HasValue && DateTime.UtcNow < _throttledUntil.Value)
                {
                    return true;
                }
                _throttledUntil = null;
                return false;
            }
        }
    }

    public DateTime? ThrottleResetAt
    {
        get
        {
            lock (_lock)
            {
                return _throttledUntil;
            }
        }
    }

    public bool CanAcquireSlot(out TimeSpan waitTime)
    {
        lock (_lock)
        {
            PruneOldTimestamps();

            if (IsThrottled)
            {
                waitTime = _throttledUntil!.Value - DateTime.UtcNow;
                if (waitTime < TimeSpan.Zero) waitTime = TimeSpan.Zero;
                return false;
            }

            if (_requestTimestamps.Count < _maxRequestsPerHour)
            {
                waitTime = TimeSpan.Zero;
                return true;
            }

            // Budget full: wait until oldest timestamp rolls out of the 1-hour window
            var oldest = _requestTimestamps.Peek();
            var expiresAt = oldest.AddHours(1);
            waitTime = expiresAt - DateTime.UtcNow;
            if (waitTime < TimeSpan.Zero) waitTime = TimeSpan.Zero;
            return false;
        }
    }

    public async Task<bool> WaitForSlotAsync(CancellationToken cancellationToken, TimeSpan? maxWait = null)
    {
        var timeoutLimit = maxWait ?? TimeSpan.FromSeconds(30);
        var startTime = DateTime.UtcNow;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (CanAcquireSlot(out var waitTime))
            {
                return true;
            }

            var elapsed = DateTime.UtcNow - startTime;
            if (elapsed + waitTime > timeoutLimit)
            {
                _logger.LogWarning("Request slot wait time ({WaitTime:N1}s) exceeds max allowed wait ({MaxWait:N1}s). Request rejected by rate limiter.",
                    waitTime.TotalSeconds, timeoutLimit.TotalSeconds);
                return false;
            }

            // Cap individual sleep intervals to 1 second for responsive cancellation
            var delayMs = (int)Math.Min(waitTime.TotalMilliseconds, 1000);
            if (delayMs <= 0) delayMs = 100;

            await Task.Delay(delayMs, cancellationToken);
        }

        return false;
    }

    public void RecordRequest()
    {
        lock (_lock)
        {
            PruneOldTimestamps();
            _requestTimestamps.Enqueue(DateTime.UtcNow);
        }
    }

    public void Record429(TimeSpan? retryAfter)
    {
        lock (_lock)
        {
            var backoff = retryAfter ?? TimeSpan.FromMinutes(5);
            _throttledUntil = DateTime.UtcNow.Add(backoff);
            _logger.LogWarning("Upstream StockChartBD returned HTTP 429. Enforcing backoff until {ResetAt:u} ({Duration:N1} mins).",
                _throttledUntil, backoff.TotalMinutes);
        }
    }

    public int GetUsedBudgetInLastHour()
    {
        lock (_lock)
        {
            PruneOldTimestamps();
            return _requestTimestamps.Count;
        }
    }

    public int GetRemainingBudget()
    {
        lock (_lock)
        {
            PruneOldTimestamps();
            var remaining = _maxRequestsPerHour - _requestTimestamps.Count;
            return Math.Max(0, remaining);
        }
    }

    private void PruneOldTimestamps()
    {
        var cutoff = DateTime.UtcNow.AddHours(-1);
        while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < cutoff)
        {
            _requestTimestamps.Dequeue();
        }
    }
}
