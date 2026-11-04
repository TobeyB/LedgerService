using LedgerService.Server.Models.Domain;

namespace LedgerService.Server.Services;

public interface IAuditService
{
    /// <summary>Audit entries where the account is either side of the booking, oldest first.</summary>
    Task<IReadOnlyList<AuditEntry>> GetAuditTrailAsync(string accountNumber, CancellationToken ct = default);
}
