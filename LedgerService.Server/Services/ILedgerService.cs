using LedgerService.Server.Models.Domain;

namespace LedgerService.Server.Services;

public interface ILedgerService
{
    Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken ct = default);

    /// <summary>Adds the amount to the account and books exactly one Deposit transaction, atomically.</summary>
    Task<LedgerResult<Transaction>> DepositAsync(string accountNumber, decimal amount, CancellationToken ct = default);
}
