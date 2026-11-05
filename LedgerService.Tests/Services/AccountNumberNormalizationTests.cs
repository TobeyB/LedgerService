using LedgerService.Server.Data;
using LedgerService.Server.Models.Domain;
using LedgerService.Server.Services;
using LedgerService.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Sut = LedgerService.Server.Services.LedgerService;

namespace LedgerService.Tests.Services;

public class AccountNumberTests
{
    [Theory]
    [InlineData("NB-1001", "NB-1001")]
    [InlineData("nb-1001", "NB-1001")]
    [InlineData("Nb-1001", "NB-1001")]
    [InlineData("  nb-1001\t", "NB-1001")]
    public void Normalize_gives_the_canonical_form(string input, string expected)
    {
        Assert.Equal(expected, AccountNumber.Normalize(input));
    }
}

/// <summary>However an account number is typed, it is processed, stored and returned in its canonical form.</summary>
[Collection(SqlServerCollection.Name)]
public class AccountNumberNormalizationTests(SqlServerFixture sql) : IAsyncLifetime
{
    private string _connectionString = null!;
    private readonly StubScreeningClient _screening = new();

    public async Task InitializeAsync()
    {
        _connectionString = await sql.CreateDatabaseAsync();
        await using var db = NewContext();
        db.Accounts.AddRange(
            Account.Open("NB-1001", "Anna", AccountType.Savings, 100_000m),
            Account.Open("NB-1002", "Erik", AccountType.Savings, 1_000m));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AppDbContext NewContext() => SqlServerFixture.CreateContext(_connectionString);

    private async Task<T> WithLedger<T>(Func<Sut, Task<T>> action)
    {
        await using var db = NewContext();
        return await action(new Sut(db, _screening));
    }

    [Fact]
    public async Task Deposit_with_a_sloppy_number_is_booked_under_the_canonical_number()
    {
        var result = await WithLedger(l => l.DepositAsync("  nb-1001 ", 10m));

        Assert.True(result.IsSuccess);
        Assert.Equal("NB-1001", result.Value!.ToAccount);

        await using var db = NewContext();
        Assert.Equal("NB-1001", (await db.Transactions.SingleAsync()).ToAccount);
        Assert.Equal("NB-1001", (await db.AuditEntries.SingleAsync()).ToAccount);
        Assert.Equal(100_010m, (await db.Accounts.SingleAsync(a => a.AccountNumber == "NB-1001")).Balance);
    }

    [Fact]
    public async Task Withdraw_with_a_lowercase_number_is_booked_under_the_canonical_number()
    {
        var result = await WithLedger(l => l.WithdrawAsync("nb-1001", 10m));

        Assert.True(result.IsSuccess);
        Assert.Equal("NB-1001", result.Value!.FromAccount);
    }

    [Fact]
    public async Task Transfer_with_lowercase_numbers_is_booked_and_screened_under_the_canonical_numbers()
    {
        var result = await WithLedger(l => l.TransferAsync("nb-1001", " Nb-1002", 12_000m));

        Assert.True(result.IsSuccess);
        Assert.Equal("NB-1001", result.Value!.FromAccount);
        Assert.Equal("NB-1002", result.Value.ToAccount);

        var call = Assert.Single(_screening.Calls);
        Assert.Equal(("NB-1001", "NB-1002"), (call.From, call.To));

        await using var db = NewContext();
        var audit = await db.AuditEntries.SingleAsync();
        Assert.Equal(("NB-1001", "NB-1002"), (audit.FromAccount, audit.ToAccount));
    }

    [Fact]
    public async Task The_same_account_in_different_spellings_is_still_the_same_account()
    {
        var result = await WithLedger(l => l.TransferAsync("NB-1001", " nb-1001", 10m));

        Assert.Equal(LedgerError.SameAccount, result.Error);
    }

    [Fact]
    public async Task A_retry_with_the_same_key_but_another_spelling_is_the_same_request()
    {
        var first = await WithLedger(l => l.TransferAsync("NB-1001", "NB-1002", 100m, "key-1"));
        var retry = await WithLedger(l => l.TransferAsync("nb-1001", "nb-1002", 100m, "key-1"));

        Assert.True(retry.IsSuccess);
        Assert.Equal(first.Value!.Id, retry.Value!.Id);

        await using var db = NewContext();
        Assert.Single(await db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task Listing_works_for_any_spelling_and_returns_canonical_numbers()
    {
        await WithLedger(l => l.DepositAsync("nb-1002", 5m));

        var transactions = (await WithLedger(l => l.GetTransactionsAsync("nB-1002"))).Value!;
        var trail = await WithLedger(async _ =>
        {
            await using var db = NewContext();
            return await new AuditService(db).GetAuditTrailAsync(" nb-1002");
        });

        Assert.Equal("NB-1002", Assert.Single(transactions).ToAccount);
        Assert.Equal("NB-1002", Assert.Single(trail).ToAccount);
    }

    [Fact]
    public async Task An_unknown_number_is_still_not_found_whatever_its_case()
    {
        Assert.Equal(LedgerError.AccountNotFound, (await WithLedger(l => l.DepositAsync("nb-9999", 1m))).Error);
        Assert.Equal(LedgerError.AccountNotFound, (await WithLedger(l => l.GetTransactionsAsync("nb-9999"))).Error);
    }
}
