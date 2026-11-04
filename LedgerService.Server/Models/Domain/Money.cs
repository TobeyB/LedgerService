namespace LedgerService.Server.Models.Domain;

/// <summary>
/// A positive SEK amount with at most two decimals. Backed by <see cref="decimal"/>, never floating point.
/// </summary>
public readonly record struct Money
{
    public const string Currency = "SEK";

    public decimal Amount { get; }

    private Money(decimal amount) => Amount = amount;

    public static bool TryCreate(decimal amount, out Money money)
    {
        money = default;

        if (amount <= 0)
            return false;

        // Rounding to 2 decimals must not change the value, otherwise it had more precision.
        if (decimal.Round(amount, 2) != amount)
            return false;

        money = new Money(amount + 0.00m); // always two decimals, so 12000 and 12000.00 look the same everywhere
        return true;
    }

    public static Money Create(decimal amount) =>
        TryCreate(amount, out var money)
            ? money
            : throw new ArgumentOutOfRangeException(
                nameof(amount), "Amount must be positive and have at most two decimals.");

    public override string ToString() => $"{Amount:0.00} {Currency}";
}
