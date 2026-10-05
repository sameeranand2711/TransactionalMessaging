namespace TransactionalMessaging.Core.Options;

/// <summary>
/// Configuration options for retry behavior.
/// Defaults follow SPEC.md section 15 recommendations.
/// </summary>
public sealed class RetryOptions
{
    /// <summary>
    /// Initial retry delay.
    /// Default: 1 second
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum retry delay (exponential backoff cap).
    /// Default: 5 minutes
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum number of retry attempts before dead-lettering.
    /// Default: 20
    /// </summary>
    public int MaxAttempts { get; set; } = 20;

    /// <summary>
    /// Exponential backoff multiplier.
    /// Default: 2.0
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Whether to apply jitter to retry delays.
    /// Default: true (full jitter)
    /// </summary>
    public bool UseJitter { get; set; } = true;
}
