using TransactionalMessaging.Core.Exceptions;
using TransactionalMessaging.Core.Models;
using TransactionalMessaging.Core.Options;

namespace TransactionalMessaging.Core.Validation;

/// <summary>
/// Validates outbox messages against configured size and content limits.
/// </summary>
public sealed class MessageValidator
{
    private readonly MessageOptions _options;

    public MessageValidator(MessageOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void Validate(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(message.MessageId))
            throw new MessageValidationException("MessageId cannot be null or empty.", nameof(message.MessageId));

        if (message.MessageId.Length > _options.MaxMessageIdLength)
            throw new MessageValidationException(
                $"MessageId exceeds maximum length of {_options.MaxMessageIdLength} characters.",
                nameof(message.MessageId));

        if (string.IsNullOrWhiteSpace(message.MessageType))
            throw new MessageValidationException("MessageType cannot be null or empty.", nameof(message.MessageType));

        if (message.MessageType.Length > _options.MaxMessageTypeLength)
            throw new MessageValidationException(
                $"MessageType exceeds maximum length of {_options.MaxMessageTypeLength} characters.",
                nameof(message.MessageType));

        if (string.IsNullOrWhiteSpace(message.MessageVersion))
            throw new MessageValidationException("MessageVersion cannot be null or empty.", nameof(message.MessageVersion));

        if (message.Payload == null || message.Payload.Length == 0)
            throw new MessageValidationException("Payload cannot be null or empty.", nameof(message.Payload));

        if (message.Payload.Length > _options.MaxPayloadSize)
            throw new MessageValidationException(
                $"Payload size {message.Payload.Length} bytes exceeds maximum of {_options.MaxPayloadSize} bytes.",
                nameof(message.Payload));

        if (string.IsNullOrWhiteSpace(message.ContentType))
            throw new MessageValidationException("ContentType cannot be null or empty.", nameof(message.ContentType));

        // Validate headers size if present
        if (message.Headers != null)
        {
            var headersSize = message.Headers.Sum(h =>
                (h.Key?.Length ?? 0) + (h.Value?.Length ?? 0));

            if (headersSize > _options.MaxHeadersSize)
                throw new MessageValidationException(
                    $"Total headers size {headersSize} bytes exceeds maximum of {_options.MaxHeadersSize} bytes.",
                    nameof(message.Headers));
        }

        // Validate ordering consistency
        if (message.OrderingKey != null && message.OrderingSequence == null)
            throw new MessageValidationException(
                "OrderingSequence must be provided when OrderingKey is set.",
                nameof(message.OrderingSequence));

        if (message.OrderingSequence != null && string.IsNullOrWhiteSpace(message.OrderingKey))
            throw new MessageValidationException(
                "OrderingKey must be provided when OrderingSequence is set.",
                nameof(message.OrderingKey));
    }
}
