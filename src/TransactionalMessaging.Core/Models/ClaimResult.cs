namespace TransactionalMessaging.Core.Models;

/// <summary>
/// Result of a claim operation containing the claimed messages and their claim token.
/// </summary>
public sealed class ClaimResult
{
    /// <summary>
    /// Messages that were successfully claimed.
    /// </summary>
    public required IReadOnlyList<OutboxMessage> Messages { get; init; }

    /// <summary>
    /// Unique claim token used for fencing.
    /// All state mutations must provide this token to prevent stale worker operations.
    /// </summary>
    public required string ClaimToken { get; init; }

    /// <summary>
    /// UTC timestamp when the claim expires.
    /// </summary>
    public required DateTime ClaimExpiresAt { get; init; }

    /// <summary>
    /// Worker identifier that owns this claim.
    /// </summary>
    public required string WorkerId { get; init; }
}
