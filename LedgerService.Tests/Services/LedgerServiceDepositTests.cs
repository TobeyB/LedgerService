using LedgerService.Server.Data;
using LedgerService.Server.Models.Domain;
using LedgerService.Server.Services;
using LedgerService.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Sut = LedgerService.Server.Services.LedgerService;

namespace LedgerService.Tests.Services;

[Collection(SqlServerCollection.Name)]
public class LedgerServiceDepositTests(SqlServerFixture sql) : IAsyncLifetime
{
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        _connectionString = await sql.CreateDatabaseAsync();
        await using var db = NewContext();
        db.Accounts.Add(Account.Open("NB-1001", "Anna", AccountType.Savings, 100m));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AppDbContext NewContext() => SqlServerFixture.CreateContext(_connectionString);

    private async Task<decimal> BalanceAsync(string accountNumber)
    {
        await using var db = NewContext();
        return await db.Accounts.Where(a => a.AccountNumber == accountNumber).Select(a => a.Balance).SingleAsync();
    }

    private async Task<List<Transaction>> TransactionsAsync()
    {
        await using var db = NewContext();
        return await db.Transactions.AsNoTracking().ToListAsync();
    }

    private async Task<List<AuditEntry>> AuditEntriesAsync()
    {
        await using var db = NewContext();
        return await db.AuditEntries.AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task Deposit_increases_balance_and_books_one_deposit_transaction()
    {
        await using var db = NewContext();

        var result = await new Sut(db).DepositAsync("NB-1001", 50.25m);

        Assert.True(result.IsSuccess);
        Assert.Equal(150.25m, await BalanceAsync("NB-1001"));

        var booked = Assert.Single(await TransactionsAsync());
        Assert.Equal(TransactionType.Deposit, booked.Type);
        Assert.Null(booked.FromAccount);
        Assert.Equal("NB-1001", booked.ToAccount);
        Assert.Equal(50.25m, booked.Amount);
        Assert.Equal("SEK", booked.Currency);

        Assert.Equal(booked.Id, result.Value!.Id);
        Assert.Equal(50.25m, result.Value.Amount);
    }

    [Fact]
    public async Task Deposit_leaves_exactly_one_audit_entry_describing_the_booking()
    {
        await using var db = NewContext();

        var result = await new Sut(db).DepositAsync("NB-1001", 50.25m);

        var entry = Assert.Single(await AuditEntriesAsync());
        Assert.Equal(result.Value!.Id, entry.TransactionId);
        Assert.Equal(TransactionType.Deposit, entry.Type);
        Assert.Null(entry.FromAccount);
        Assert.Equal("NB-1001", entry.ToAccount);
        Assert.Equal(50.25m, entry.Amount);
        Assert.Equal("SEK", entry.Currency);
        Assert.Equal(result.Value.BookedAt, entry.BookedAt);
    }

    [Fact]
    public async Task Audit_entry_and_transaction_are_in_the_same_database_transaction()
    {
        // The audit insert is made to fail by renaming its table, so the booking must then roll back entirely.
        await using (var setup = NewContext())
            await setup.Database.ExecuteSqlRawAsync("EXEC sp_rename 'AuditEntries', 'AuditEntries_broken'");

        await using var db = NewContext();
        await Assert.ThrowsAnyAsync<Exception>(() => new Sut(db).DepositAsync("NB-1001", 10m));

        Assert.Equal(100m, await BalanceAsync("NB-1001"));
        Assert.Empty(await TransactionsAsync());
    }

    [Fact]
    public async Task Database_refuses_a_second_audit_entry_for_the_same_transaction()
    {
        await using var db = NewContext();
        var booked = (await new Sut(db).DepositAsync("NB-1001", 10m)).Value!;

        await using var other = NewContext();
        other.AuditEntries.Add(AuditEntry.From(booked));

        await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());
        Assert.Single(await AuditEntriesAsync());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-10")]
    [InlineData("10.001")]
    [InlineData("0.001")]
    public async Task Deposit_with_invalid_amount_is_rejected_and_changes_nothing(string amount)
    {
        await using var db = NewContext();

        var result = await new Sut(db).DepositAsync("NB-1001", decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(LedgerError.InvalidAmount, result.Error);
        Assert.Equal(100m, await BalanceAsync("NB-1001"));
        Assert.Empty(await TransactionsAsync());
        Assert.Empty(await AuditEntriesAsync());
    }

    [Fact]
    public async Task Deposit_to_unknown_account_fails_and_books_nothing()
    {
        await using var db = NewContext();

        var result = await new Sut(db).DepositAsync("NB-9999", 10m);

        Assert.Equal(LedgerError.AccountNotFound, result.Error);
        Assert.Empty(await TransactionsAsync());
        Assert.Empty(await AuditEntriesAsync());
    }

    [Fact]
    public async Task Many_concurrent_deposits_are_all_applied_exactly_once()
    {
        const int count = 100;

        // Each request gets its own context, like separate HTTP requests would.
        await Task.WhenAll(Enumerable.Range(0, count).Select(async _ =>
        {
            await using var db = NewContext();
            var result = await new Sut(db).DepositAsync("NB-1001", 10.10m);
            Assert.True(result.IsSuccess);
        }));

        Assert.Equal(100m + count * 10.10m, await BalanceAsync("NB-1001"));
        var transactions = await TransactionsAsync();
        Assert.Equal(count, transactions.Count);

        // Exactly one audit entry per booked transaction, no more, no fewer.
        var audit = await AuditEntriesAsync();
        Assert.Equal(count, audit.Count);
        Assert.Equal(
            transactions.Select(t => t.Id).Order(),
            audit.Select(a => a.TransactionId).Order());
    }

    [Fact]
    public async Task Balance_equals_opening_balance_plus_booked_transactions()
    {
        await Task.WhenAll(Enumerable.Range(1, 20).Select(async i =>
        {
            await using var db = NewContext();
            await new Sut(db).DepositAsync("NB-1001", i);
        }));

        var booked = await TransactionsAsync();
        Assert.Equal(100m + booked.Sum(t => t.Amount), await BalanceAsync("NB-1001"));
    }
}
