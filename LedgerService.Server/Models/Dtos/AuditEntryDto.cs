using LedgerService.Server.Models.Domain;

namespace LedgerService.Server.Models.Dtos;

public record AuditEntryDto(
    Guid TransactionId,
    TransactionType Type,
    string? FromAccount,
    string? ToAccount,
    decimal Amount,
    string Currency,
    DateTimeOffset BookedAt,
    DateTimeOffset RecordedAt)
{
    public static AuditEntryDto From(AuditEntry e) =>
        new(e.TransactionId, e.Type, e.FromAccount, e.ToAccount, e.Amount, e.Currency, e.BookedAt, e.RecordedAt);
}
