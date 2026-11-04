using LedgerService.Server.Data;
using LedgerService.Server.Models.Domain;
using LedgerService.Server.Services.Screening;
using Microsoft.EntityFrameworkCore;

namespace LedgerService.Server.Services;

public partial class LedgerService(AppDbContext db, IScreeningClient screening) : ILedgerService
{
    public async Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken ct = default) =>
        await db.Accounts
            .AsNoTracking()
            .OrderBy(a => a.AccountNumber)
            .ToListAsync(ct);

    public async Task<LedgerResult<Transaction>> DepositAsync(
        string accountNumber, decimal amount, CancellationToken ct = default)
    {
        if (!Money.TryCreate(amount, out var money))
            return LedgerResult<Transaction>.Fail(LedgerError.InvalidAmount);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var updated = await db.Accounts
            .Where(a => a.AccountNumber == accountNumber)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Balance, a => a.Balance + money.Amount), ct);

        if (updated == 0)
            return LedgerResult<Transaction>.Fail(LedgerError.AccountNotFound);

        var bookedTransaction = new Transaction
        {
            Type = TransactionType.Deposit,
            ToAccount = accountNumber,
            Amount = money.Amount
        };
        db.Transactions.Add(bookedTransaction);
        db.AuditEntries.Add(AuditEntry.From(bookedTransaction));
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return LedgerResult<Transaction>.Success(bookedTransaction);
    }

    public async Task<LedgerResult<Transaction>> WithdrawAsync(
        string accountNumber, decimal amount, CancellationToken ct = default)
    {
        if (!Money.TryCreate(amount, out var money))
            return LedgerResult<Transaction>.Fail(LedgerError.InvalidAmount);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var updated = await db.Accounts
            .Where(a => a.AccountNumber == accountNumber && a.Balance >= money.Amount)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Balance, a => a.Balance - money.Amount), ct);

        if (updated == 0)
        {
            var exists = await db.Accounts.AnyAsync(a => a.AccountNumber == accountNumber, ct);
            return LedgerResult<Transaction>.Fail(exists ? LedgerError.InsufficientFunds : LedgerError.AccountNotFound);
        }

        var bookedTransaction = new Transaction
        {
            Type = TransactionType.Withdrawal,
            FromAccount = accountNumber,
            Amount = money.Amount
        };
        db.Transactions.Add(bookedTransaction);
        db.AuditEntries.Add(AuditEntry.From(bookedTransaction));
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return LedgerResult<Transaction>.Success(bookedTransaction);
    }
}
