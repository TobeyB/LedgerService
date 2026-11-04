namespace LedgerService.Server.Models.Domain;

/// <summary>
/// Remembers the outcome of a request made with an Idempotency-Key so retries get the same answer.
/// The key is the primary key, so two concurrent first requests cannot both claim it.
/// </summary>
public class IdempotencyRecord
{
    public required string Key { get; init; }

    /// <summary>Hash of the request body. A retry with the same key but another body is a client error.</summary>
    public required string RequestHash { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Null while the first request is still in progress.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    public int? ResponseStatusCode { get; set; }

    public string? ResponseBody { get; set; }
}
