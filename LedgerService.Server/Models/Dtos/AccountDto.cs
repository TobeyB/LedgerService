using LedgerService.Server.Models.Domain;

namespace LedgerService.Server.Models.Dtos;

public record AccountDto(
    string AccountNumber,
    string Owner,
    AccountType Type,
    decimal Balance,
    string Currency)
{
    public static AccountDto From(Account a) =>
        new(a.AccountNumber, a.Owner, a.Type, a.Balance, a.Currency);
}
