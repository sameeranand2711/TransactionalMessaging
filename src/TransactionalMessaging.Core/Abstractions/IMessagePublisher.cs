using TransactionalMessaging.Core.Models;

namespace TransactionalMessaging.Core.Abstractions;

/// <summary>
/// Abstraction for publishing messages to a broker/transport.
/// Implementations adapter to Kafka, RabbitMQ, or other messaging systems.
/// Core does not depend on broker-specific libraries (SPEC.md section 19).
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Publishes a message to the configured broker/transport.
    /// Returns a result indicating success or categorized failure.
    /// Throws for unexpected errors that should not be retried.
    /// </summary>
    Task<PublicationResult> PublishAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default);
}
