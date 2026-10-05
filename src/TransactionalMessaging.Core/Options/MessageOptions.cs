namespace TransactionalMessaging.Core.Options;

/// <summary>
/// Configuration options for message size limits and validation.
/// Defaults follow SPEC.md section 10 safety limits.
/// </summary>
public sealed class MessageOptions
{
    /// <summary>
    /// Default maximum payload size in bytes.
    /// Default: 256 KiB
    /// </summary>
    public int DefaultMaxPayloadSize { get; set; } = 256 * 1024;

    /// <summary>
    /// Hard library ceiling for payload size in bytes.
    /// Cannot exceed 1 MiB.
    /// </summary>
    public int MaxPayloadSize { get; set; } = 1024 * 1024;

    /// <summary>
    /// Maximum total size for message headers in bytes.
    /// Default: 16 KiB
    /// </summary>
    public int MaxHeadersSize { get; set; } = 16 * 1024;

    /// <summary>
    /// Maximum length for MessageId.
    /// Default: 128 characters
    /// </summary>
    public int MaxMessageIdLength { get; set; } = 128;

    /// <summary>
    /// Maximum length for MessageType.
    /// Default: 256 characters
    /// </summary>
    public int MaxMessageTypeLength { get; set; } = 256;
}
