namespace TransactionalMessaging.Core.Models;

/// <summary>
/// Represents the processing state of an inbox message.
/// </summary>
public enum InboxState
{
    /// <summary>
    /// Message has been received and reserved for processing.
    /// </summary>
    Reserved = 0,

    /// <summary>
    /// Message processing completed successfully.
    /// Duplicate deliveries will be suppressed.
    /// </summary>
    Completed = 1,

    /// <summary>
    /// Message processing failed and will not be retried.
    /// </summary>
    Failed = 2
}
