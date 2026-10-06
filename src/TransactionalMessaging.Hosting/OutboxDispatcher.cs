using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransactionalMessaging.Core.Abstractions;
using TransactionalMessaging.Core.Internal;
using TransactionalMessaging.Core.Models;
using TransactionalMessaging.Core.Options;

namespace TransactionalMessaging.Hosting;

/// <summary>
/// Background service that dispatches outbox messages to the configured publisher.
/// Implements SPEC.md section 14 publish flow with bounded batching, lease renewal,
/// retry/backoff, and fencing.
/// </summary>
public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IOutboxStore _store;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly DispatcherOptions _dispatcherOptions;
    private readonly RetryOptions _retryOptions;

    public OutboxDispatcher(
        IOutboxStore store,
        IMessagePublisher publisher,
        IOptions<DispatcherOptions> dispatcherOptions,
        IOptions<RetryOptions> retryOptions,
        ILogger<OutboxDispatcher> logger)
    {
        _store = store;
        _publisher = publisher;
        _logger = logger;
        _dispatcherOptions = dispatcherOptions.Value;
        _retryOptions = retryOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OutboxDispatcher starting: WorkerId={WorkerId}, BatchSize={BatchSize}, LeaseDuration={LeaseDuration}, MaxConcurrency={MaxConcurrency}",
            _dispatcherOptions.WorkerId,
            _dispatcherOptions.BatchSize,
            _dispatcherOptions.LeaseDuration,
            _dispatcherOptions.MaxConcurrency);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("OutboxDispatcher stopping due to cancellation");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in OutboxDispatcher loop");
                await Task.Delay(_dispatcherOptions.ErrorBackoffDelay, stoppingToken);
            }
        }

        _logger.LogInformation("OutboxDispatcher stopped");
    }

    private async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Step 1: Claim a bounded batch in a short DB transaction (SPEC.md section 14)
        ClaimResult? claimResult;
        try
        {
            claimResult = await _store.ClaimBatchAsync(
                now,
                _dispatcherOptions.WorkerId,
                _dispatcherOptions.BatchSize,
                _dispatcherOptions.LeaseDuration,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to claim batch from outbox store");
            await Task.Delay(_dispatcherOptions.ErrorBackoffDelay, cancellationToken);
            return;
        }

        // No messages available
        if (claimResult.Messages.Count == 0)
        {
            await Task.Delay(_dispatcherOptions.PollingInterval, cancellationToken);
            return;
        }

        _logger.LogDebug(
            "Claimed {Count} messages with token {ClaimToken}, expires at {ExpiresAt:O}",
            claimResult.Messages.Count,
            claimResult.ClaimToken,
            claimResult.ClaimExpiresAt);

        // Step 2: Commit claim transaction (already committed by ClaimBatchAsync)
        // Step 3: Publish outside the DB transaction
        await PublishBatchAsync(claimResult, cancellationToken);
    }

    private async Task PublishBatchAsync(ClaimResult claimResult, CancellationToken cancellationToken)
    {
        // Bounded parallel publish (SPEC.md section 21)
        var semaphore = new SemaphoreSlim(_dispatcherOptions.MaxConcurrency);
        var tasks = new List<Task>();

        foreach (var message in claimResult.Messages)
        {
            await semaphore.WaitAsync(cancellationToken);

            var task = Task.Run(async () =>
            {
                try
                {
                    await PublishMessageAsync(message, claimResult.ClaimToken, claimResult.ClaimExpiresAt, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
    }

    private async Task PublishMessageAsync(
        OutboxMessage message,
        string claimToken,
        DateTime claimExpiresAt,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if lease renewal needed (SPEC.md section 13)
            var halfLeaseRemaining = (claimExpiresAt - DateTime.UtcNow).TotalMilliseconds;
            if (halfLeaseRemaining < _dispatcherOptions.LeaseDuration.TotalMilliseconds / 2)
            {
                var newExpiresAt = DateTime.UtcNow.Add(_dispatcherOptions.LeaseDuration);
                var renewed = await _store.RenewLeaseAsync(
                    message.MessageId,
                    claimToken,
                    newExpiresAt,
                    cancellationToken);

                if (renewed)
                {
                    _logger.LogDebug("Renewed lease for message {MessageId} until {NewExpiresAt:O}",
                        message.MessageId, newExpiresAt);
                }
                else
                {
                    _logger.LogWarning("Failed to renew lease for message {MessageId} - claim may have expired",
                        message.MessageId);
                    return;
                }
            }

            // Step 3: Publish to broker
            var result = await _publisher.PublishAsync(message, cancellationToken);

            // Step 4: Broker acknowledged
            // Step 5: Mark Published using ClaimToken (SPEC.md section 14)
            if (result.Success)
            {
                var marked = await _store.MarkPublishedAsync(
                    message.MessageId,
                    claimToken,
                    DateTime.UtcNow,
                    cancellationToken);

                if (marked)
                {
                    _logger.LogInformation(
                        "Published message {MessageId} of type {MessageType}",
                        message.MessageId,
                        message.MessageType);
                }
                else
                {
                    _logger.LogWarning(
                        "Published message {MessageId} but failed to mark as published (stale claim token) - will be republished",
                        message.MessageId);
                }
            }
            else
            {
                await HandlePublicationFailureAsync(message, claimToken, result, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Publication cancelled for message {MessageId}", message.MessageId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error publishing message {MessageId}", message.MessageId);
            await ScheduleRetryOrDeadLetterAsync(
                message,
                claimToken,
                "UNEXPECTED_ERROR",
                ex.Message,
                isRetryable: true,
                cancellationToken);
        }
    }

    private async Task HandlePublicationFailureAsync(
        OutboxMessage message,
        string claimToken,
        PublicationResult result,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Failed to publish message {MessageId}: {ErrorCode} - {ErrorMessage}, Retryable={IsRetryable}",
            message.MessageId,
            result.ErrorCode,
            result.ErrorMessage,
            result.IsRetryable);

        await ScheduleRetryOrDeadLetterAsync(
            message,
            claimToken,
            result.ErrorCode ?? "UNKNOWN_ERROR",
            result.ErrorMessage ?? "Publication failed",
            result.IsRetryable,
            cancellationToken);
    }

    private async Task ScheduleRetryOrDeadLetterAsync(
        OutboxMessage message,
        string claimToken,
        string errorCode,
        string errorSummary,
        bool isRetryable,
        CancellationToken cancellationToken)
    {
        var newAttemptCount = message.AttemptCount + 1;

        // Check if max attempts reached (SPEC.md section 15)
        if (!isRetryable || newAttemptCount >= _retryOptions.MaxAttempts)
        {
            var moved = await _store.MoveToDeadLetterAsync(
                message.MessageId,
                claimToken,
                errorCode,
                errorSummary,
                cancellationToken);

            if (moved)
            {
                _logger.LogWarning(
                    "Moved message {MessageId} to dead letter after {AttemptCount} attempts. Reason: {ErrorCode}",
                    message.MessageId,
                    newAttemptCount,
                    errorCode);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to move message {MessageId} to dead letter (stale claim token)",
                    message.MessageId);
            }
        }
        else
        {
            // Calculate exponential backoff with jitter (SPEC.md section 15)
            var retryDelay = RetryDelayCalculator.CalculateDelay(newAttemptCount, _retryOptions);
            var nextAttemptAt = DateTime.UtcNow.Add(retryDelay);

            var scheduled = await _store.ScheduleRetryAsync(
                message.MessageId,
                claimToken,
                nextAttemptAt,
                newAttemptCount,
                errorCode,
                errorSummary,
                cancellationToken);

            if (scheduled)
            {
                _logger.LogDebug(
                    "Scheduled retry for message {MessageId} at {NextAttemptAt:O} (attempt {AttemptCount}/{MaxAttempts})",
                    message.MessageId,
                    nextAttemptAt,
                    newAttemptCount,
                    _retryOptions.MaxAttempts);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to schedule retry for message {MessageId} (stale claim token)",
                    message.MessageId);
            }
        }
    }
}
