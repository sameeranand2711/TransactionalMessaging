using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransactionalMessaging.Core.Abstractions;
using TransactionalMessaging.Hosting.Options;

namespace TransactionalMessaging.Hosting;

/// <summary>
/// Background service that periodically deletes old Published outbox messages.
/// Implements SPEC.md section 22 cleanup requirements.
/// Never touches Pending, Claimed, or DeadLettered messages.
/// </summary>
public sealed class OutboxCleanupService : BackgroundService
{
    private readonly IOutboxStore _store;
    private readonly ILogger<OutboxCleanupService> _logger;
    private readonly CleanupOptions _options;

    public OutboxCleanupService(
        IOutboxStore store,
        IOptions<CleanupOptions> options,
        ILogger<OutboxCleanupService> logger)
    {
        _store = store;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("OutboxCleanupService is disabled");
            return;
        }

        _logger.LogInformation(
            "OutboxCleanupService starting: Interval={Interval}, RetentionPeriod={RetentionPeriod}, BatchSize={BatchSize}",
            _options.CleanupInterval,
            _options.PublishedRetentionPeriod,
            _options.BatchSize);

        // Wait for initial delay before first cleanup
        await Task.Delay(_options.CleanupInterval, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupPublishedMessagesAsync(stoppingToken);
                await Task.Delay(_options.CleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("OutboxCleanupService stopping due to cancellation");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during outbox cleanup");
                await Task.Delay(_options.CleanupInterval, stoppingToken);
            }
        }

        _logger.LogInformation("OutboxCleanupService stopped");
    }

    private async Task CleanupPublishedMessagesAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var olderThan = startedAt.Subtract(_options.PublishedRetentionPeriod);
        var totalDeleted = 0;

        _logger.LogDebug("Starting cleanup of Published messages older than {OlderThan:O}", olderThan);

        try
        {
            // Delete in bounded batches (SPEC.md section 22)
            int deletedCount;
            do
            {
                deletedCount = await _store.CleanupPublishedAsync(
                    olderThan,
                    _options.BatchSize,
                    cancellationToken);

                totalDeleted += deletedCount;

                if (deletedCount > 0)
                {
                    _logger.LogDebug("Deleted {Count} published messages", deletedCount);
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
                    "Cleanup completed: deleted {TotalDeleted} Published messages in {Duration:0.00}s",
                    totalDeleted,
                    duration.TotalSeconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup published messages");
        }
    }
}
