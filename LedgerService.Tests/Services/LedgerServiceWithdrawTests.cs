using System.Globalization;
using LedgerService.Server.Data;
using LedgerService.Server.Models.Domain;
using LedgerService.Server.Services;
using LedgerService.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Sut = LedgerService.Server.Services.LedgerService;

namespace LedgerService.Tests.Services;

[Collection(SqlServerCollection.Name)]
public class LedgerServiceWithdrawTests(SqlServerFixture sql) : IAsyncLifetime
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
    public async Task Withdraw_decreases_balance_and_books_one_withdrawal_with_one_audit_entry()
    {
        await using var db = NewContext();

        var result = await new Sut(db).WithdrawAsync("NB-1001", 40.25m);

        Assert.True(result.IsSuccess);
        Assert.Equal(59.75m, await BalanceAsync("NB-1001"));

        var booked = Assert.Single(await TransactionsAsync());
        Assert.Equal(TransactionType.Withdrawal, booked.Type);
        Assert.Equal("NB-1001", booked.FromAccount);
        Assert.Null(booked.ToAccount);
        Assert.Equal(40.25m, booked.Amount);
        Assert.Equal(booked.Id, result.Value!.Id);

        var entry = Assert.Single(await AuditEntriesAsync());
        Assert.Equal(booked.Id, entry.TransactionId);
        Assert.Equal(TransactionType.Withdrawal, entry.Type);
        Assert.Equal("NB-1001", entry.FromAccount);
        Assert.Equal(40.25m, entry.Amount);
    }

    [Fact]
    public async Task Withdrawing_the_entire_balance_is_allowed_and_leaves_zero()
    {
        await using var db = NewContext();

        var result = await new Sut(db).WithdrawAsync("NB-1001", 100m);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, await BalanceAsync("NB-1001"));
    }

    [Theory]
    [InlineData("100.01")]
    [InlineData("1000000")]
    public async Task Withdraw_more_than_the_balance_is_refused_and_changes_nothing(string amount)
    {
        await using var db = NewContext();

        var result = await new Sut(db).WithdrawAsync("NB-1001", decimal.Parse(amount, CultureInfo.InvariantCulture));

        Assert.Equal(LedgerError.InsufficientFunds, result.Error);
        Assert.Equal(100m, await BalanceAsync("NB-1001"));
        Assert.Empty(await TransactionsAsync());
        Assert.Empty(await AuditEntriesAsync());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-10")]
    [InlineData("10.001")]
    public async Task Withdraw_with_invalid_amount_is_rejected_and_changes_nothing(string amount)
    {
        await using var db = NewContext();

        var result = await new Sut(db).WithdrawAsync("NB-1001", decimal.Parse(amount, CultureInfo.InvariantCulture));

        Assert.Equal(LedgerError.InvalidAmount, result.Error);
        Assert.Equal(100m, await BalanceAsync("NB-1001"));
        Assert.Empty(await TransactionsAsync());
        Assert.Empty(await AuditEntriesAsync());
    }

    [Fact]
    public async Task Withdraw_from_unknown_account_fails_and_books_nothing()
    {
        await using var db = NewContext();

        var result = await new Sut(db).WithdrawAsync("NB-9999", 10m);

        Assert.Equal(LedgerError.AccountNotFound, result.Error);
        Assert.Empty(await TransactionsAsync());
        Assert.Empty(await AuditEntriesAsync());
    }

    [Fact]
    public async Task Concurrent_withdrawals_never_overdraw_the_account()
    {
        // 100 SEK available, 50 requests of 10 SEK: exactly 10 may succeed.
        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(async _ =>
        {
            await using var db = NewContext();
            return await new Sut(db).WithdrawAsync("NB-1001", 10m);
        }));

        Assert.Equal(10, results.Count(r => r.IsSuccess));
        Assert.Equal(40, results.Count(r => r.Error == LedgerError.InsufficientFunds));
        Assert.Equal(0m, await BalanceAsync("NB-1001"));

        var transactions = await TransactionsAsync();
        Assert.Equal(10, transactions.Count);
        var audit = await AuditEntriesAsync();
        Assert.Equal(transactions.Select(t => t.Id).Order(), audit.Select(a => a.TransactionId).Order());
    }

    [Fact]
    public async Task Concurrent_deposits_and_withdrawals_keep_balance_equal_to_the_booked_history()
    {
        var work = Enumerable.Range(0, 60).Select(async i =>
        {
            await using var db = NewContext();
            var ledger = new Sut(db);
            if (i % 2 == 0)
                await ledger.DepositAsync("NB-1001", 7.50m);
            else
                await ledger.WithdrawAsync("NB-1001", 12.25m);
        });
        await Task.WhenAll(work);

        var transactions = await TransactionsAsync();
        var expected = 100m + transactions.Sum(t => t.Type == TransactionType.Deposit ? t.Amount : -t.Amount);

        var balance = await BalanceAsync("NB-1001");
        Assert.Equal(expected, balance);
        Assert.True(balance >= 0);
        Assert.Equal(transactions.Count, (await AuditEntriesAsync()).Count);
    }
}
