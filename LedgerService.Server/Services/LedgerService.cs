using LedgerService.Server.Data;
using LedgerService.Server.Models.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LedgerService.Server.Services;

public class LedgerService(AppDbContext db) : ILedgerService
{
    public async Task<IReadOnlyList<AccountDto>> GetAccountsAsync(CancellationToken ct = default) =>
        await db.Accounts
            .AsNoTracking()
            .OrderBy(a => a.AccountNumber)
            .Select(a => new AccountDto(a.AccountNumber, a.Owner, a.Type, a.Balance, a.Currency))
            .ToListAsync(ct);
}
