namespace TransactionalMessaging.Hosting.Options;

/// <summary>
/// Configuration options for the inbox cleanup service.
/// Retention must be at least as long as the maximum plausible redelivery window
/// (SPEC.md section 22). Deleting too early re-enables duplicate business execution.
/// </summary>
public sealed class InboxCleanupOptions
{
    /// <summary>
    /// Whether inbox cleanup is enabled.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How long to retain Completed inbox messages before deletion.
    /// Must be at least as long as the maximum plausible redelivery window.
    /// Default: 30 days
    /// </summary>
    public TimeSpan CompletedRetentionPeriod { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// How often to run the cleanup process.
    /// Default: 1 hour
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Number of messages to delete in each batch.
    /// Default: 1000
    /// </summary>
    public int BatchSize { get; set; } = 1000;
}
