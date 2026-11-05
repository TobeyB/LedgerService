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

    /// <summary>
    /// Moves the full amount between two different accounts, or nothing. Transfers of 10 000 SEK or more are screened first;
    /// anything but a clear approval (rejected, unreachable, slow) means no transfer.
    /// With an <paramref name="idempotencyKey"/> the transfer happens at most once and every retry gets the first answer,
    /// except ScreeningUnavailable, which is not remembered so the client can try again.
    /// </summary>
    Task<LedgerResult<Transaction>> TransferAsync(
        string fromAccount, string toAccount, decimal amount, string? idempotencyKey = null, CancellationToken ct = default);

    /// <summary>
    /// Booked transactions where the account is either side, newest first. Fails with AccountNotFound for an unknown account.
    /// </summary>
    Task<LedgerResult<IReadOnlyList<Transaction>>> GetTransactionsAsync(string accountNumber, CancellationToken ct = default);
}
