namespace ShareSync.Application.Interfaces;

/// <summary>
/// Enforces a rolling request-budget on calls to the StockChartBD public API.
/// Documented provider rate limit is 120 requests/hour/IP.
/// Tracks hourly usage, handles HTTP 429 Retry-After, and prevents request storms.
/// </summary>
public interface IStockChartBdRateLimiter
{
    /// <summary>
    /// Checks if a request slot is immediately available within the rolling 1-hour window.
    /// </summary>
    bool CanAcquireSlot(out TimeSpan waitTime);

    /// <summary>
    /// Awaits until a request slot becomes available, or returns false if maxWait is exceeded.
    /// </summary>
    Task<bool> WaitForSlotAsync(CancellationToken cancellationToken, TimeSpan? maxWait = null);

    /// <summary>
    /// Records an executed outbound request against the rolling hourly budget.
    /// </summary>
    void RecordRequest();

    /// <summary>
    /// Records an HTTP 429 rate limit response from the upstream server and activates backoff.
    /// </summary>
    void Record429(TimeSpan? retryAfter);

    /// <summary>
    /// Current number of requests dispatched within the rolling 60-minute window.
    /// </summary>
    int GetUsedBudgetInLastHour();

    /// <summary>
    /// Remaining request budget within the current hourly window.
    /// </summary>
    int GetRemainingBudget();

    /// <summary>
    /// True if an active HTTP 429 cooldown is currently in effect.
    /// </summary>
    bool IsThrottled { get; }

    /// <summary>
    /// Time when the current 429 throttle cools down, if throttled.
    /// </summary>
    DateTime? ThrottleResetAt { get; }
}
