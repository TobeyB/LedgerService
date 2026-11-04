using LedgerService.Server.Models.Domain;

namespace LedgerService.Server.Services;

public interface ILedgerService
{
    Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken ct = default);

    /// <summary>Adds the amount to the account and books exactly one Deposit transaction, atomically.</summary>
    Task<LedgerResult<Transaction>> DepositAsync(string accountNumber, decimal amount, CancellationToken ct = default);

    /// <summary>
    /// Takes the full amount from the account or nothing, and books exactly one Withdrawal transaction, atomically.
    /// Fails with InsufficientFunds rather than letting the balance go negative.
    /// </summary>
    Task<LedgerResult<Transaction>> WithdrawAsync(string accountNumber, decimal amount, CancellationToken ct = default);
}
