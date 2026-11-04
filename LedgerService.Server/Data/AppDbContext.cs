using LedgerService.Server.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace LedgerService.Server.Data;

/// <summary>Primary store: balances, transactions, idempotency records and the audit outbox.</summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<AuditOutboxItem> AuditOutbox => Set<AuditOutboxItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Account>(e =>
        {
            e.ToTable("Accounts", t =>
            {
                // Last line of defence: even a bug in the service cannot persist a negative balance.
                t.HasCheckConstraint("CK_Accounts_Balance_NonNegative", "[Balance] >= 0");
                t.HasCheckConstraint("CK_Accounts_Currency_SEK", "[Currency] = 'SEK'");
            });
            e.HasKey(a => a.AccountNumber);
            e.Property(a => a.AccountNumber).HasMaxLength(32);
            e.Property(a => a.Owner).HasMaxLength(200).IsRequired();
            e.Property(a => a.Balance).HasPrecision(18, 2);
            e.Property(a => a.Currency).HasMaxLength(3).IsRequired();
        });

        b.Entity<Transaction>(e =>
        {
            e.ToTable("Transactions", t =>
            {
                t.HasCheckConstraint("CK_Transactions_Amount_Positive", "[Amount] > 0");
                t.HasCheckConstraint("CK_Transactions_Accounts_MatchType",
                    "([Type] = 1 AND [FromAccount] IS NULL AND [ToAccount] IS NOT NULL) " +
                    "OR ([Type] = 2 AND [FromAccount] IS NOT NULL AND [ToAccount] IS NULL) " +
                    "OR ([Type] = 3 AND [FromAccount] IS NOT NULL AND [ToAccount] IS NOT NULL AND [FromAccount] <> [ToAccount])");
            });
            e.HasKey(t => t.Id);
            e.Property(t => t.FromAccount).HasMaxLength(32);
            e.Property(t => t.ToAccount).HasMaxLength(32);
            e.Property(t => t.Amount).HasPrecision(18, 2);
            e.Property(t => t.Currency).HasMaxLength(3).IsRequired();
            e.Property(t => t.IdempotencyKey).HasMaxLength(200);

            e.HasOne<Account>().WithMany().HasForeignKey(t => t.FromAccount).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Account>().WithMany().HasForeignKey(t => t.ToAccount).OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(t => t.FromAccount);
            e.HasIndex(t => t.ToAccount);
            // At most one booked transaction per idempotency key (filtered: many NULLs allowed).
            e.HasIndex(t => t.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        });

        b.Entity<IdempotencyRecord>(e =>
        {
            e.ToTable("IdempotencyRecords");
            e.HasKey(r => r.Key);
            e.Property(r => r.Key).HasMaxLength(200);
            e.Property(r => r.RequestHash).HasMaxLength(64).IsRequired();
        });

        b.Entity<AuditOutboxItem>(e =>
        {
            e.ToTable("AuditOutbox");
            e.HasKey(o => o.Id);
            e.HasIndex(o => o.TransactionId).IsUnique();
            // Worker scans for pending items.
            e.HasIndex(o => o.ProcessedAt);
            e.HasOne<Transaction>().WithMany().HasForeignKey(o => o.TransactionId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
