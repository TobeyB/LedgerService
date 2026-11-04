namespace LedgerService.Server.Models.Domain;

/// <summary>
/// Written in the same database transaction as the booked <see cref="Transaction"/>, so a booked
/// transaction can never lose its audit record. A background worker copies pending items to the
/// audit store and marks them processed; the audit store de-duplicates on TransactionId.
/// </summary>
public class AuditOutboxItem
{
    public long Id { get; set; }

    public required Guid TransactionId { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ProcessedAt { get; set; }

    public int Attempts { get; set; }
}
