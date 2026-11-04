namespace LedgerService.Server.Models.Domain;

/// <summary>
/// Kind of account. More types will come; no type-specific behaviour is defined yet.
/// Values are explicit so they stay stable when persisted.
/// </summary>
public enum AccountType
{
    Savings = 1,
    FixedTerm = 2,
    Business = 3
}
