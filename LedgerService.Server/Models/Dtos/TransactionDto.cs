using LedgerService.Server.Models.Domain;

namespace LedgerService.Server.Models.Dtos;

public record TransactionDto(
    Guid Id,
    TransactionType Type,
    string? FromAccount,
    string? ToAccount,
    decimal Amount,
    string Currency,
    DateTimeOffset BookedAt)
{
    public static TransactionDto From(Transaction t) =>
        new(t.Id, t.Type, t.FromAccount, t.ToAccount, t.Amount, t.Currency, t.BookedAt);
}
