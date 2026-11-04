namespace LedgerService.Server.Models.Domain;

/// <summary>
/// Audit record for exactly one booked transaction. Lives in a separate store that may be down,
/// so it is written after booking and must be idempotent on <see cref="TransactionId"/> (unique).
/// Deliberately holds no personal data (no owner names) — account numbers only.
/// </summary>
public class AuditEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The booked transaction this entry describes. Unique: one entry per transaction.</summary>
    public required Guid TransactionId { get; init; }

    public required TransactionType Type { get; init; }

    public string? FromAccount { get; init; }

    public string? ToAccount { get; init; }

    public required decimal Amount { get; init; }

    public string Currency { get; init; } = Money.Currency;

    /// <summary>When the underlying transaction was booked.</summary>
    public required DateTimeOffset BookedAt { get; init; }

    /// <summary>When the audit entry itself was written (may lag if the audit store was down).</summary>
    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;

    public static AuditEntry From(Transaction t) => new()
    {
        TransactionId = t.Id,
        Type = t.Type,
        FromAccount = t.FromAccount,
        ToAccount = t.ToAccount,
        Amount = t.Amount,
        Currency = t.Currency,
        BookedAt = t.BookedAt
    };
}
