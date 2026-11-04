namespace LedgerService.Server.Models.Domain;

public class Account
{
    /// <summary>Business key, e.g. NB-1001.</summary>
    public required string AccountNumber { get; init; }

    public required string Owner { get; init; }

    public required AccountType Type { get; init; }

    public decimal Balance { get; private set; }

    public string Currency { get; init; } = Money.Currency;

    public static Account Open(string accountNumber, string owner, AccountType type, decimal openingBalance = 0m)
    {
        if (openingBalance < 0 || decimal.Round(openingBalance, 2) != openingBalance)
            throw new ArgumentOutOfRangeException(nameof(openingBalance));

        return new Account
        {
            AccountNumber = accountNumber,
            Owner = owner,
            Type = type,
            Balance = openingBalance
        };
    }

    public void Credit(Money amount) => Balance += amount.Amount;

    /// <summary>Returns false and leaves the balance untouched if funds are insufficient.</summary>
    public bool TryDebit(Money amount)
    {
        if (Balance < amount.Amount)
            return false;

        Balance -= amount.Amount;
        return true;
    }
}
