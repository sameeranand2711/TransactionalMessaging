using TransactionalMessaging.Core.Exceptions;
using TransactionalMessaging.Core.Models;
using TransactionalMessaging.Core.Options;
using TransactionalMessaging.Core.Validation;
using Xunit;

namespace TransactionalMessaging.Core.Tests.Validation;

public class MessageValidatorTests
{
    private readonly MessageValidator _validator;
    private readonly MessageOptions _options;

    public MessageValidatorTests()
    {
        _options = new MessageOptions();
        _validator = new MessageValidator(_options);
    }

    [Fact]
    public void Validate_ValidMessage_DoesNotThrow()
    {
        var message = CreateValidMessage();

        var exception = Record.Exception(() => _validator.Validate(message));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_NullMessageId_ThrowsValidationException()
    {
        var message = new OutboxMessage
        {
            MessageId = null!,
            MessageType = "test.event",
            MessageVersion = "1",
            Payload = System.Text.Encoding.UTF8.GetBytes("{}"),
            ContentType = "application/json",
            State = OutboxState.Pending,
            AttemptCount = 0,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var exception = Assert.Throws<MessageValidationException>(() => _validator.Validate(message));

        Assert.Contains("MessageId", exception.Message);
    }

    [Fact]
    public void Validate_EmptyMessageId_ThrowsValidationException()
    {
        var message = new OutboxMessage
        {
            MessageId = "",
            MessageType = "test.event",
            MessageVersion = "1",
            Payload = System.Text.Encoding.UTF8.GetBytes("{}"),
            ContentType = "application/json",
            State = OutboxState.Pending,
            AttemptCount = 0,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var exception = Assert.Throws<MessageValidationException>(() => _validator.Validate(message));

        Assert.Contains("MessageId", exception.Message);
    }

    [Fact]
    public void Validate_MessageIdTooLong_ThrowsValidationException()
    {
        var message = new OutboxMessage
        {
            MessageId = new string('a', 129),
            MessageType = "test.event",
            MessageVersion = "1",
            Payload = System.Text.Encoding.UTF8.GetBytes("{}"),
            ContentType = "application/json",
            State = OutboxState.Pending,
            AttemptCount = 0,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var exception = Assert.Throws<MessageValidationException>(() => _validator.Validate(message));

        Assert.Contains("MessageId", exception.Message);
        Assert.Contains("maximum length", exception.Message);
    }

    [Fact]
    public void Validate_PayloadExceedsMaxSize_ThrowsValidationException()
    {
        var largePayload = new byte[_options.MaxPayloadSize + 1];
        var message = new OutboxMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            MessageType = "test.event",
            MessageVersion = "1",
            Payload = largePayload,
            ContentType = "application/json",
            State = OutboxState.Pending,
            AttemptCount = 0,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var exception = Assert.Throws<MessageValidationException>(() => _validator.Validate(message));

        Assert.Contains("Payload", exception.Message);
        Assert.Contains("exceeds maximum", exception.Message);
    }

    [Fact]
    public void Validate_OrderingKeyWithoutSequence_ThrowsValidationException()
    {
        var message = new OutboxMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            MessageType = "test.event",
            MessageVersion = "1",
            Payload = System.Text.Encoding.UTF8.GetBytes("{}"),
            ContentType = "application/json",
            State = OutboxState.Pending,
            AttemptCount = 0,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            OrderingKey = "wallet:123",
            OrderingSequence = null
        };

        var exception = Assert.Throws<MessageValidationException>(() => _validator.Validate(message));

        Assert.Contains("OrderingSequence", exception.Message);
    }

    [Fact]
    public void Validate_OrderingSequenceWithoutKey_ThrowsValidationException()
    {
        var message = new OutboxMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            MessageType = "test.event",
            MessageVersion = "1",
            Payload = System.Text.Encoding.UTF8.GetBytes("{}"),
            ContentType = "application/json",
            State = OutboxState.Pending,
            AttemptCount = 0,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            OrderingKey = null,
            OrderingSequence = 1
        };

        var exception = Assert.Throws<MessageValidationException>(() => _validator.Validate(message));

        Assert.Contains("OrderingKey", exception.Message);
    }

    [Fact]
    public void Validate_BothOrderingKeyAndSequence_DoesNotThrow()
    {
        var message = new OutboxMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            MessageType = "test.event",
            MessageVersion = "1",
            Payload = System.Text.Encoding.UTF8.GetBytes("{}"),
            ContentType = "application/json",
            State = OutboxState.Pending,
            AttemptCount = 0,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            OrderingKey = "wallet:123",
            OrderingSequence = 42
        };

        var exception = Record.Exception(() => _validator.Validate(message));

        Assert.Null(exception);
    }

    private static OutboxMessage CreateValidMessage() => new()
    {
        MessageId = Guid.NewGuid().ToString("N"),
        MessageType = "test.event",
        MessageVersion = "1",
        Payload = System.Text.Encoding.UTF8.GetBytes("{}"),
        ContentType = "application/json",
        State = OutboxState.Pending,
        AttemptCount = 0,
        OccurredAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow
    };
}
