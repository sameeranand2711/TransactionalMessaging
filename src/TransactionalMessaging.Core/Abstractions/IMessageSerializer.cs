namespace TransactionalMessaging.Core.Abstractions;

/// <summary>
/// Abstraction for message payload serialization.
/// Default implementation uses UTF-8 JSON (SPEC.md section 10).
/// </summary>
public interface IMessageSerializer
{
    /// <summary>
    /// Serializes a message payload to bytes.
    /// </summary>
    byte[] Serialize<T>(T message);

    /// <summary>
    /// Deserializes a message payload from bytes.
    /// </summary>
    T Deserialize<T>(byte[] payload);

    /// <summary>
    /// Gets the content type for serialized payloads.
    /// Default: "application/json; charset=utf-8"
    /// </summary>
    string ContentType { get; }
}
