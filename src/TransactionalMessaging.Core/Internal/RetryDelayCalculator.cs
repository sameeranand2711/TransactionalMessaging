using TransactionalMessaging.Core.Options;

namespace TransactionalMessaging.Core.Internal;

/// <summary>
/// Calculates retry delays using exponential backoff with optional jitter.
/// Follows SPEC.md section 15 recommendations.
/// </summary>
internal static class RetryDelayCalculator
{
    private static readonly Random Random = new();

    public static TimeSpan CalculateDelay(int attemptCount, RetryOptions options)
    {
        if (attemptCount <= 0)
            return options.InitialDelay;

        // Exponential backoff: initialDelay * (multiplier ^ (attemptCount - 1))
        var delayMs = options.InitialDelay.TotalMilliseconds
            * Math.Pow(options.BackoffMultiplier, attemptCount - 1);

        // Cap at max delay
        delayMs = Math.Min(delayMs, options.MaxDelay.TotalMilliseconds);

        // Apply full jitter if enabled
        if (options.UseJitter)
        {
            lock (Random)
            {
                delayMs = Random.NextDouble() * delayMs;
            }
        }

        return TimeSpan.FromMilliseconds(delayMs);
    }
}
