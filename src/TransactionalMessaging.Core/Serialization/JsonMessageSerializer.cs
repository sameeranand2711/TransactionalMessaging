using System.Text.Json;
using TransactionalMessaging.Core.Abstractions;

namespace TransactionalMessaging.Core.Serialization;

/// <summary>
/// Default UTF-8 JSON message serializer using System.Text.Json.
/// </summary>
public sealed class JsonMessageSerializer : IMessageSerializer
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly JsonSerializerOptions _options;

    public JsonMessageSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? DefaultOptions;
    }

    public byte[] Serialize<T>(T message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return JsonSerializer.SerializeToUtf8Bytes(message, _options);
    }

    public T Deserialize<T>(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return JsonSerializer.Deserialize<T>(payload, _options)
            ?? throw new InvalidOperationException("Deserialization resulted in null.");
    }

    public string ContentType => "application/json; charset=utf-8";
}
