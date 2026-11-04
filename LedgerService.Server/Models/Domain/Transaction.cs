namespace LedgerService.Server.Models.Domain;

/// <summary>
/// A booked deposit, withdrawal or transfer. Immutable once written, and only ever
/// written in the same database transaction as the balance change it describes.
/// </summary>
public class Transaction
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required TransactionType Type { get; init; }

    /// <summary>Debited account. Null for deposits.</summary>
    public string? FromAccount { get; init; }

    /// <summary>Credited account. Null for withdrawals.</summary>
    public string? ToAccount { get; init; }

    public required decimal Amount { get; init; }

    public string Currency { get; init; } = Money.Currency;

    public DateTimeOffset BookedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Idempotency key of the request that created this transaction, if any.</summary>
    public string? IdempotencyKey { get; init; }

    public bool Involves(string accountNumber) =>
        FromAccount == accountNumber || ToAccount == accountNumber;
}
