namespace TransactionalMessaging.Core.Options;

/// <summary>
/// Configuration options for the outbox dispatcher.
/// Defaults follow SPEC.md section 13 recommendations.
/// </summary>
public sealed class DispatcherOptions
{
    /// <summary>
    /// Number of messages to claim in each batch.
    /// Default: 100
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Duration of the claim lease.
    /// Default: 2 minutes
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Maximum number of concurrent publication operations.
    /// Default: min(ProcessorCount, 8)
    /// </summary>
    public int MaxConcurrency { get; set; } = Math.Min(Environment.ProcessorCount, 8);

    /// <summary>
    /// Delay between dispatch loop iterations when no messages are available.
    /// Default: 1 second
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Backoff delay when the database or broker is unavailable.
    /// Default: 10 seconds
    /// </summary>
    public TimeSpan ErrorBackoffDelay { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Unique identifier for this dispatcher worker.
    /// Defaults to machine name + process ID.
    /// </summary>
    public string WorkerId { get; set; } = $"{Environment.MachineName}-{Environment.ProcessId}";
}
