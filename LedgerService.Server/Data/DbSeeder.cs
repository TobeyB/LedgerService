using LedgerService.Server.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace LedgerService.Server.Data;

/// <summary>Development seed data. All names are fictional.</summary>
public static class DbSeeder
{
    /// <summary>The company the fake screening service always rejects.</summary>
    public const string RejectedCompanyAccount = "NB-2001";

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Accounts.AnyAsync(ct))
            return;

        db.Accounts.AddRange(
            Account.Open("NB-1001", "Anna Lindqvist", AccountType.Savings, 50_000.00m),
            Account.Open("NB-1002", "Erik Johansson", AccountType.Savings, 12_500.50m),
            Account.Open("NB-1003", "Maria Svensson", AccountType.FixedTerm, 100_000.00m),
            Account.Open("NB-1004", "Nordic Widgets AB", AccountType.Business, 250_000.00m),
            Account.Open(RejectedCompanyAccount, "Shadow Holdings AB", AccountType.Business, 500_000.00m));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another copy of the service may have seeded at the same time. That is fine; anything else is not.
            if (!await db.Accounts.AsNoTracking().AnyAsync(ct))
                throw;
        }
    }
}
