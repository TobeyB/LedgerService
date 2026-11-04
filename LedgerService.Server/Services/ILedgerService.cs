using LedgerService.Server.Models.Dtos;

namespace LedgerService.Server.Services;

public interface ILedgerService
{
    Task<IReadOnlyList<AccountDto>> GetAccountsAsync(CancellationToken ct = default);
}
