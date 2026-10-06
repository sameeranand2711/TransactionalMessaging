namespace TransactionalMessaging.Hosting.Options;

/// <summary>
/// Configuration options for the outbox cleanup service.
/// Defaults follow SPEC.md section 22 recommendations.
/// </summary>
public sealed class CleanupOptions
{
    /// <summary>
    /// Whether cleanup is enabled.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How long to retain Published messages before deletion.
    /// Default: 7 days (SPEC.md section 22)
    /// </summary>
    public TimeSpan PublishedRetentionPeriod { get; set; } = TimeSpan.FromDays(7);

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
