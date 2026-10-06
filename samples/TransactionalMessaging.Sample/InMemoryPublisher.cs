using System.Collections.Concurrent;
using TransactionalMessaging.Core.Abstractions;
using TransactionalMessaging.Core.Models;

namespace TransactionalMessaging.Sample;

/// <summary>
/// Simple in-memory publisher for demonstration purposes.
/// Stores published messages in a concurrent queue for verification.
/// </summary>
public sealed class InMemoryPublisher : IMessagePublisher
{
    private readonly ConcurrentQueue<OutboxMessage> _publishedMessages = new();

    public IReadOnlyCollection<OutboxMessage> PublishedMessages => _publishedMessages.ToArray();

    public Task<PublicationResult> PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        Console.WriteLine($"[Publisher] Publishing message {message.MessageId} of type '{message.MessageType}'");

        _publishedMessages.Enqueue(message);

        return Task.FromResult(new PublicationResult
        {
            Success = true,
            MessageId = message.MessageId
        });
    }
}
