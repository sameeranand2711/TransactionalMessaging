using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransactionalMessaging.Core.Abstractions;
using TransactionalMessaging.Hosting.Options;

namespace TransactionalMessaging.Hosting;

/// <summary>
/// Background service that periodically deletes old Completed inbox messages.
/// Implements SPEC.md section 22 cleanup requirements for inbox.
/// Retention must be at least as long as the maximum plausible redelivery window.
/// </summary>
public sealed class InboxCleanupService : BackgroundService
{
    private readonly IInboxStore _store;
    private readonly ILogger<InboxCleanupService> _logger;
    private readonly InboxCleanupOptions _options;

    public InboxCleanupService(
        IInboxStore store,
        IOptions<InboxCleanupOptions> options,
        ILogger<InboxCleanupService> logger)
    {
        _store = store;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("InboxCleanupService is disabled");
            return;
        }

        _logger.LogInformation(
            "InboxCleanupService starting: Interval={Interval}, RetentionPeriod={RetentionPeriod}, BatchSize={BatchSize}",
            _options.CleanupInterval,
            _options.CompletedRetentionPeriod,
            _options.BatchSize);

        // Wait for initial delay before first cleanup
        await Task.Delay(_options.CleanupInterval, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupCompletedMessagesAsync(stoppingToken);
                await Task.Delay(_options.CleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("InboxCleanupService stopping due to cancellation");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during inbox cleanup");
                await Task.Delay(_options.CleanupInterval, stoppingToken);
            }
        }

        _logger.LogInformation("InboxCleanupService stopped");
    }

    private async Task CleanupCompletedMessagesAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var olderThan = startedAt.Subtract(_options.CompletedRetentionPeriod);
        var totalDeleted = 0;

        _logger.LogDebug("Starting cleanup of Completed inbox messages older than {OlderThan:O}", olderThan);

        try
        {
            // Delete in bounded batches
            int deletedCount;
            do
            {
                deletedCount = await _store.CleanupCompletedAsync(
                    olderThan,
                    _options.BatchSize,
                    cancellationToken);

                totalDeleted += deletedCount;

                if (deletedCount > 0)
                {
                    _logger.LogDebug("Deleted {Count} completed inbox messages", deletedCount);
                }

                // Small delay between batches to reduce database load
                if (deletedCount == _options.BatchSize)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                }
            }
            while (deletedCount > 0 && !cancellationToken.IsCancellationRequested);

            var duration = DateTime.UtcNow - startedAt;

            if (totalDeleted > 0)
            {
                _logger.LogInformation(
                    "Inbox cleanup completed: deleted {TotalDeleted} Completed messages in {Duration:0.00}s",
                    totalDeleted,
                    duration.TotalSeconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup completed inbox messages");
        }
    }
}
