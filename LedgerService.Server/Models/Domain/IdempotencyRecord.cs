namespace LedgerService.Server.Models.Domain;

/// <summary>
/// Remembers the outcome of a request made with an Idempotency-Key so retries get the same answer.
/// The key is the primary key, so two concurrent first requests cannot both claim it.
/// </summary>
public class IdempotencyRecord
{
    public required string Key { get; init; }

    /// <summary>Hash of the request. A retry with the same key but another request is a client error.</summary>
    public required string RequestHash { get; init; }

    /// <summary>When the request was claimed. An unfinished claim that is old enough is treated as abandoned.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Null while the first request is still in progress.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>The booked transaction, if the request succeeded. Set in the same database transaction as the booking.</summary>
    public Guid? ResultTransactionId { get; set; }

    /// <summary>The business error, if the request was definitively refused. Stored as the service's error code.</summary>
    public int? ResultError { get; set; }
}
