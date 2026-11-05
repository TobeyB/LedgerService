using LedgerService.Server.Data;
using LedgerService.Server.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace LedgerService.Server.Services;

public class AuditService(AppDbContext db) : IAuditService
{
    public async Task<IReadOnlyList<AuditEntry>> GetAuditTrailAsync(string accountNumber, CancellationToken ct = default)
    {
        accountNumber = AccountNumber.Normalize(accountNumber);

        return await db.AuditEntries
            .AsNoTracking()
            .Where(a => a.FromAccount == accountNumber || a.ToAccount == accountNumber)
            .OrderBy(a => a.BookedAt)
            .ThenBy(a => a.Id)
            .ToListAsync(ct);
    }
}
