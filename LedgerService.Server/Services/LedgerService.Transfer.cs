using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LedgerService.Server.Models.Domain;
using LedgerService.Server.Services.Screening;
using Microsoft.EntityFrameworkCore;

namespace LedgerService.Server.Services;

// Transfer: validation, idempotency, screening and the atomic booking.
public partial class LedgerService
{
    /// <summary>Transfers of at least this amount must be approved by the screening service before money moves.</summary>
    public const decimal ScreeningThreshold = 10_000m;

    /// <summary>An unfinished idempotency claim older than this is assumed to belong to a crashed request.</summary>
    private static readonly TimeSpan ClaimAbandonedAfter = TimeSpan.FromMinutes(2);

    public async Task<LedgerResult<Transaction>> TransferAsync(
        string fromAccount, string toAccount, decimal amount, string? idempotencyKey = null, CancellationToken ct = default)
    {
        fromAccount = AccountNumber.Normalize(fromAccount);
        toAccount = AccountNumber.Normalize(toAccount);

        if (!Money.TryCreate(amount, out var money))
            return LedgerResult<Transaction>.Fail(LedgerError.InvalidAmount);

        if (fromAccount == toAccount)
            return LedgerResult<Transaction>.Fail(LedgerError.SameAccount);

        // Without a key nothing is remembered, so a retry is a new transfer. The id only identifies this attempt to screening.
        if (idempotencyKey is null)
            return await ScreenAndBookAsync(fromAccount, toAccount, money, null, Guid.NewGuid().ToString("N"), ct);

        var refused = await ClaimAsync(idempotencyKey, HashRequest(fromAccount, toAccount, money), ct);
        if (refused is not null)
            return refused;

        var finished = false;
        try
        {
            // The key doubles as the screening request id, so every retry gets the same decision and is not billed again.
            var result = await ScreenAndBookAsync(fromAccount, toAccount, money, idempotencyKey, idempotencyKey, ct);

            if (result.IsSuccess)
            {
                finished = true; // the record was completed in the same database transaction as the booking
            }
            else if (result.Error != LedgerError.ScreeningUnavailable)
            {
                // A definitive refusal is remembered, so retries get the same answer.
                await CompleteWithErrorAsync(idempotencyKey, result.Error!.Value, ct);
                finished = true;
            }

            return result;
        }
        catch (DbUpdateException)
        {
            // Another request with the same key booked it first (the unique key on transactions caught us). Replay its answer.
            var record = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(r => r.Key == idempotencyKey, ct);
            if (record?.CompletedAt is null)
                throw;
            finished = true;
            return await ReplayAsync(record, ct);
        }
        finally
        {
            // Not finished (screening unavailable, or an unexpected failure): let the client retry for real.
            if (!finished)
                await ReleaseClaimAsync(idempotencyKey);
        }
    }

    private async Task<LedgerResult<Transaction>> ScreenAndBookAsync(
        string from, string to, Money money, string? idempotencyKey, string screeningRequestId, CancellationToken ct)
    {
        if (money.Amount >= ScreeningThreshold)
        {
            // Cheap check first, so a transfer that cannot work is never sent to (and billed by) screening.
            var accounts = await db.Accounts.AsNoTracking()
                .Where(a => a.AccountNumber == from || a.AccountNumber == to)
                .Select(a => new { a.AccountNumber, a.Balance })
                .ToListAsync(ct);
            var source = accounts.FirstOrDefault(a => a.AccountNumber == from);
            var target = accounts.FirstOrDefault(a => a.AccountNumber == to);

            if (source is null || target is null)
                return LedgerResult<Transaction>.Fail(LedgerError.AccountNotFound);
            if (source.Balance < money.Amount)
                return LedgerResult<Transaction>.Fail(LedgerError.InsufficientFunds);

            // No money has moved yet. Anything but a clear approval means no transfer.
            var outcome = await screening.ScreenAsync(screeningRequestId, from, to, money.Amount, ct);
            if (outcome == ScreeningOutcome.Rejected)
                return LedgerResult<Transaction>.Fail(LedgerError.ScreeningRejected);
            if (outcome != ScreeningOutcome.Approved)
                return LedgerResult<Transaction>.Fail(LedgerError.ScreeningUnavailable);
        }

        return await BookTransferAsync(from, to, money, idempotencyKey, ct);
    }

    private async Task<LedgerResult<Transaction>> BookTransferAsync(
        string from, string to, Money money, string? idempotencyKey, CancellationToken ct)
    {
        var amount = money.Amount;

        // Everything below commits together or not at all: both balances, the transaction, its audit entry
        // and the idempotency record. There is no moment where money has left one account but not arrived in the other.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        Task<int> Debit() => db.Accounts
            .Where(a => a.AccountNumber == from && a.Balance >= amount)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Balance, a => a.Balance - amount), ct);

        Task<int> Credit() => db.Accounts
            .Where(a => a.AccountNumber == to)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Balance, a => a.Balance + amount), ct);

        // Always touch the account with the lower number first, so two transfers in opposite directions
        // between the same accounts wait for each other instead of deadlocking.
        int debited, credited;
        if (string.CompareOrdinal(from, to) < 0)
        {
            debited = await Debit();
            credited = await Credit();
        }
        else
        {
            credited = await Credit();
            debited = await Debit();
        }

        // Returning without committing rolls back whichever update did succeed.
        if (credited == 0)
            return LedgerResult<Transaction>.Fail(LedgerError.AccountNotFound);
        if (debited == 0)
        {
            var exists = await db.Accounts.AnyAsync(a => a.AccountNumber == from, ct);
            return LedgerResult<Transaction>.Fail(exists ? LedgerError.InsufficientFunds : LedgerError.AccountNotFound);
        }

        var bookedTransaction = new Transaction
        {
            Type = TransactionType.Transfer,
            FromAccount = from,
            ToAccount = to,
            Amount = amount,
            IdempotencyKey = idempotencyKey
        };
        db.Transactions.Add(bookedTransaction);
        db.AuditEntries.Add(AuditEntry.From(bookedTransaction));
        await db.SaveChangesAsync(ct);

        if (idempotencyKey is not null)
        {
            await db.IdempotencyRecords
                .Where(r => r.Key == idempotencyKey)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.CompletedAt, DateTimeOffset.UtcNow)
                    .SetProperty(r => r.ResultTransactionId, bookedTransaction.Id), ct);
        }

        await tx.CommitAsync(ct);
        return LedgerResult<Transaction>.Success(bookedTransaction);
    }

    /// <summary>Claims the key. Returns null if this request now owns it, otherwise the answer to give instead.</summary>
    private async Task<LedgerResult<Transaction>?> ClaimAsync(string key, string requestHash, CancellationToken ct)
    {
        var record = new IdempotencyRecord { Key = key, RequestHash = requestHash };
        db.IdempotencyRecords.Add(record);
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateException)
        {
            db.Entry(record).State = EntityState.Detached;
            var existing = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(r => r.Key == key, ct);
            if (existing is null)
                throw; // not a duplicate key: a real failure

            if (existing.RequestHash != requestHash)
                return LedgerResult<Transaction>.Fail(LedgerError.IdempotencyKeyReused);

            if (existing.CompletedAt is not null)
                return await ReplayAsync(existing, ct);

            if (DateTimeOffset.UtcNow - existing.CreatedAt > ClaimAbandonedAfter)
            {
                // The first request seems to have died. Take over, but only if nobody else just did.
                var taken = await db.IdempotencyRecords
                    .Where(r => r.Key == key && r.CompletedAt == null && r.CreatedAt == existing.CreatedAt)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.CreatedAt, DateTimeOffset.UtcNow), ct);
                if (taken == 1)
                    return null;
            }

            return LedgerResult<Transaction>.Fail(LedgerError.RequestInProgress);
        }
    }

    private async Task<LedgerResult<Transaction>> ReplayAsync(IdempotencyRecord record, CancellationToken ct)
    {
        if (record.ResultError is { } error)
            return LedgerResult<Transaction>.Fail((LedgerError)error);

        var booked = await db.Transactions.AsNoTracking().SingleAsync(t => t.Id == record.ResultTransactionId, ct);
        return LedgerResult<Transaction>.Success(booked);
    }

    private Task CompleteWithErrorAsync(string key, LedgerError error, CancellationToken ct) =>
        db.IdempotencyRecords
            .Where(r => r.Key == key && r.CompletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.CompletedAt, DateTimeOffset.UtcNow)
                .SetProperty(r => r.ResultError, (int)error), ct);

    private Task ReleaseClaimAsync(string key) =>
        db.IdempotencyRecords
            .Where(r => r.Key == key && r.CompletedAt == null)
            .ExecuteDeleteAsync(CancellationToken.None);

    internal static string HashRequest(string from, string to, Money money)
    {
        var canonical = string.Create(CultureInfo.InvariantCulture,
            $"{from}|{to}|{money.Amount:F2}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
