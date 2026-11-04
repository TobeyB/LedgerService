using System.Globalization;
using LedgerService.Server.Data;
using LedgerService.Server.Models.Domain;
using LedgerService.Server.Services;
using LedgerService.Server.Services.Screening;
using LedgerService.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Sut = LedgerService.Server.Services.LedgerService;

namespace LedgerService.Tests.Services;

[Collection(SqlServerCollection.Name)]
public class LedgerServiceTransferTests(SqlServerFixture sql) : IAsyncLifetime
{
    private const decimal OpeningTotal = 100_000m + 1_000m + 50m;

    private string _connectionString = null!;
    private readonly StubScreeningClient _screening = new();

    public async Task InitializeAsync()
    {
        _connectionString = await sql.CreateDatabaseAsync();
        await using var db = NewContext();
        db.Accounts.AddRange(
            Account.Open("NB-1001", "Anna", AccountType.Savings, 100_000m),
            Account.Open("NB-1002", "Erik", AccountType.Savings, 1_000m),
            Account.Open("NB-1003", "Maria", AccountType.Savings, 50m));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AppDbContext NewContext() => SqlServerFixture.CreateContext(_connectionString);

    /// <summary>Each call is a separate request: its own context and service, like separate HTTP requests.</summary>
    private async Task<LedgerResult<Transaction>> TransferAsync(
        string from, string to, decimal amount, string? key = null, CancellationToken ct = default)
    {
        await using var db = NewContext();
        return await new Sut(db, _screening).TransferAsync(from, to, amount, key, ct);
    }

    private async Task<decimal> BalanceAsync(string accountNumber)
    {
        await using var db = NewContext();
        return await db.Accounts.Where(a => a.AccountNumber == accountNumber).Select(a => a.Balance).SingleAsync();
    }

    private async Task<decimal> TotalAsync()
    {
        await using var db = NewContext();
        return await db.Accounts.SumAsync(a => a.Balance);
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

    private async Task AssertNothingBookedAsync(decimal anna = 100_000m, decimal erik = 1_000m)
    {
        Assert.Equal(anna, await BalanceAsync("NB-1001"));
        Assert.Equal(erik, await BalanceAsync("NB-1002"));
        Assert.Empty(await TransactionsAsync());
        Assert.Empty(await AuditEntriesAsync());
    }

    // ---------- The basics ----------

    [Fact]
    public async Task Transfer_moves_the_full_amount_and_books_one_transfer_with_one_audit_entry()
    {
        var result = await TransferAsync("NB-1001", "NB-1002", 250.75m);

        Assert.True(result.IsSuccess);
        Assert.Equal(100_000m - 250.75m, await BalanceAsync("NB-1001"));
        Assert.Equal(1_000m + 250.75m, await BalanceAsync("NB-1002"));
        Assert.Equal(OpeningTotal, await TotalAsync()); // money is never created or destroyed

        var booked = Assert.Single(await TransactionsAsync());
        Assert.Equal(TransactionType.Transfer, booked.Type);
        Assert.Equal("NB-1001", booked.FromAccount);
        Assert.Equal("NB-1002", booked.ToAccount);
        Assert.Equal(250.75m, booked.Amount);
        Assert.Equal(booked.Id, result.Value!.Id);

        var entry = Assert.Single(await AuditEntriesAsync());
        Assert.Equal(booked.Id, entry.TransactionId);
        Assert.Equal(TransactionType.Transfer, entry.Type);
        Assert.Equal("NB-1001", entry.FromAccount);
        Assert.Equal("NB-1002", entry.ToAccount);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1.001")]
    public async Task Invalid_amounts_are_rejected_and_nothing_changes(string amount)
    {
        var result = await TransferAsync("NB-1001", "NB-1002", decimal.Parse(amount, CultureInfo.InvariantCulture));

        Assert.Equal(LedgerError.InvalidAmount, result.Error);
        await AssertNothingBookedAsync();
    }

    [Theory]
    [InlineData("NB-1001", "NB-1001")]
    [InlineData("NB-1001", "nb-1001")]
    public async Task Transfer_to_the_same_account_is_refused(string from, string to)
    {
        var result = await TransferAsync(from, to, 10m);

        Assert.Equal(LedgerError.SameAccount, result.Error);
        await AssertNothingBookedAsync();
    }

    [Fact]
    public async Task Transfer_of_more_than_the_balance_is_refused_and_nothing_moves()
    {
        var result = await TransferAsync("NB-1002", "NB-1001", 1_000.01m);

        Assert.Equal(LedgerError.InsufficientFunds, result.Error);
        await AssertNothingBookedAsync();
    }

    [Fact]
    public async Task Transferring_the_entire_balance_is_allowed()
    {
        var result = await TransferAsync("NB-1002", "NB-1001", 1_000m);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, await BalanceAsync("NB-1002"));
    }

    [Theory]
    [InlineData("NB-1001", "NB-9999")]
    [InlineData("NB-9999", "NB-1001")]
    public async Task Unknown_account_is_refused_and_nothing_moves_on_the_other_side(string from, string to)
    {
        var result = await TransferAsync(from, to, 100m);

        Assert.Equal(LedgerError.AccountNotFound, result.Error);
        await AssertNothingBookedAsync(); // in particular: not debited when the credit side does not exist
    }

    // ---------- Screening ----------

    [Fact]
    public async Task Below_the_threshold_screening_is_not_called()
    {
        var result = await TransferAsync("NB-1001", "NB-1002", 9_999.99m);

        Assert.True(result.IsSuccess);
        Assert.Empty(_screening.Calls);
    }

    [Fact]
    public async Task At_the_threshold_the_transfer_is_screened_before_money_moves_and_goes_through_when_approved()
    {
        var result = await TransferAsync("NB-1001", "NB-1002", 10_000m);

        Assert.True(result.IsSuccess);
        var call = Assert.Single(_screening.Calls);
        Assert.Equal(("NB-1001", "NB-1002", 10_000m), (call.From, call.To, call.Amount));
        Assert.Equal(90_000m, await BalanceAsync("NB-1001"));
        Assert.Equal(11_000m, await BalanceAsync("NB-1002"));
    }

    [Fact]
    public async Task A_rejected_screening_refuses_the_transfer_and_nothing_moves()
    {
        _screening.Outcome = ScreeningOutcome.Rejected;

        var result = await TransferAsync("NB-1001", "NB-1002", 20_000m);

        Assert.Equal(LedgerError.ScreeningRejected, result.Error);
        await AssertNothingBookedAsync();
    }

    [Fact]
    public async Task When_screening_cannot_be_reached_no_transfer_is_made()
    {
        _screening.Outcome = ScreeningOutcome.Unavailable;

        var result = await TransferAsync("NB-1001", "NB-1002", 20_000m);

        Assert.Equal(LedgerError.ScreeningUnavailable, result.Error);
        await AssertNothingBookedAsync();
    }

    [Fact]
    public async Task A_transfer_that_cannot_work_is_not_sent_to_screening()
    {
        // Screening is billed per call, so impossible transfers must not reach it.
        await TransferAsync("NB-1002", "NB-1001", 20_000m); // insufficient funds
        await TransferAsync("NB-1001", "NB-9999", 20_000m); // unknown account

        Assert.Empty(_screening.Calls);
    }

    [Fact]
    public async Task Money_does_not_move_while_screening_is_still_deciding()
    {
        _screening.Delay = TimeSpan.FromMilliseconds(400);

        var transfer = TransferAsync("NB-1001", "NB-1002", 20_000m);
        await Task.Delay(150);

        Assert.Equal(100_000m, await BalanceAsync("NB-1001")); // screening has not answered yet
        Assert.True((await transfer).IsSuccess);
        Assert.Equal(80_000m, await BalanceAsync("NB-1001"));
    }

    // ---------- Idempotency ----------

    [Fact]
    public async Task A_retried_transfer_happens_once_and_every_retry_gets_the_same_answer()
    {
        var first = await TransferAsync("NB-1001", "NB-1002", 12_000m, key: "key-1");
        var retry1 = await TransferAsync("NB-1001", "NB-1002", 12_000m, key: "key-1");
        var retry2 = await TransferAsync("NB-1001", "NB-1002", 12_000m, key: "key-1");

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value!.Id, retry1.Value!.Id);
        Assert.Equal(first.Value.Id, retry2.Value!.Id);
        Assert.Equal(first.Value.BookedAt, retry2.Value.BookedAt);
        Assert.Equal(first.Value.Amount.ToString(CultureInfo.InvariantCulture), retry2.Value.Amount.ToString(CultureInfo.InvariantCulture));

        Assert.Equal(88_000m, await BalanceAsync("NB-1001")); // charged once, not three times
        Assert.Equal(13_000m, await BalanceAsync("NB-1002"));
        Assert.Single(await TransactionsAsync());
        Assert.Single(await AuditEntriesAsync());
        Assert.Single(_screening.Calls); // the retries were not screened (or billed) again
        Assert.Equal("key-1", (await TransactionsAsync()).Single().IdempotencyKey);
    }

    [Fact]
    public async Task The_screening_request_id_is_the_idempotency_key()
    {
        await TransferAsync("NB-1001", "NB-1002", 12_000m, key: "key-xyz");

        Assert.Equal(["key-xyz"], _screening.Calls.Select(c => c.RequestId));
    }

    [Fact]
    public async Task The_same_key_with_a_different_request_is_refused()
    {
        await TransferAsync("NB-1001", "NB-1002", 100m, key: "key-1");

        var other = await TransferAsync("NB-1001", "NB-1002", 200m, key: "key-1");
        var otherTarget = await TransferAsync("NB-1001", "NB-1003", 100m, key: "key-1");

        Assert.Equal(LedgerError.IdempotencyKeyReused, other.Error);
        Assert.Equal(LedgerError.IdempotencyKeyReused, otherTarget.Error);
        Assert.Equal(99_900m, await BalanceAsync("NB-1001"));
        Assert.Single(await TransactionsAsync());
    }

    [Fact]
    public async Task Different_keys_are_different_transfers()
    {
        await TransferAsync("NB-1001", "NB-1002", 100m, key: "key-1");
        await TransferAsync("NB-1001", "NB-1002", 100m, key: "key-2");

        Assert.Equal(2, (await TransactionsAsync()).Count);
        Assert.Equal(99_800m, await BalanceAsync("NB-1001"));
    }

    [Fact]
    public async Task Without_a_key_every_request_is_a_new_transfer()
    {
        await TransferAsync("NB-1001", "NB-1002", 100m);
        await TransferAsync("NB-1001", "NB-1002", 100m);

        Assert.Equal(2, (await TransactionsAsync()).Count);
    }

    [Fact]
    public async Task A_refusal_is_remembered_so_the_retry_gets_the_same_answer_even_if_things_changed()
    {
        var first = await TransferAsync("NB-1002", "NB-1001", 5_000m, key: "key-1");
        Assert.Equal(LedgerError.InsufficientFunds, first.Error);

        await using (var db = NewContext())
            await new Sut(db, _screening).DepositAsync("NB-1002", 50_000m); // now there is enough

        var retry = await TransferAsync("NB-1002", "NB-1001", 5_000m, key: "key-1");

        Assert.Equal(LedgerError.InsufficientFunds, retry.Error);
        Assert.DoesNotContain(await TransactionsAsync(), t => t.Type == TransactionType.Transfer);
    }

    [Fact]
    public async Task A_screening_rejection_is_remembered_and_not_asked_again()
    {
        _screening.Outcome = ScreeningOutcome.Rejected;
        var first = await TransferAsync("NB-1001", "NB-1002", 20_000m, key: "key-1");

        _screening.Outcome = ScreeningOutcome.Approved; // would approve now, but the answer to this key is fixed
        var retry = await TransferAsync("NB-1001", "NB-1002", 20_000m, key: "key-1");

        Assert.Equal(LedgerError.ScreeningRejected, first.Error);
        Assert.Equal(LedgerError.ScreeningRejected, retry.Error);
        Assert.Single(_screening.Calls);
        await AssertNothingBookedAsync();
    }

    [Fact]
    public async Task A_screening_outage_is_not_remembered_so_the_retry_can_succeed_once_it_is_back()
    {
        _screening.Outcome = ScreeningOutcome.Unavailable;
        var first = await TransferAsync("NB-1001", "NB-1002", 20_000m, key: "key-1");

        _screening.Outcome = ScreeningOutcome.Approved;
        var retry = await TransferAsync("NB-1001", "NB-1002", 20_000m, key: "key-1");

        Assert.Equal(LedgerError.ScreeningUnavailable, first.Error);
        Assert.True(retry.IsSuccess);
        Assert.Equal(80_000m, await BalanceAsync("NB-1001"));
        Assert.Single(await TransactionsAsync());
    }

    [Fact]
    public async Task An_unexpected_failure_releases_the_key_so_the_client_can_retry()
    {
        _screening.Throws = new InvalidOperationException("boom");
        await Assert.ThrowsAsync<InvalidOperationException>(() => TransferAsync("NB-1001", "NB-1002", 20_000m, key: "key-1"));

        _screening.Throws = null;
        var retry = await TransferAsync("NB-1001", "NB-1002", 20_000m, key: "key-1");

        Assert.True(retry.IsSuccess);
        Assert.Single(await TransactionsAsync());
    }

    [Fact]
    public async Task While_the_first_request_is_still_running_a_retry_with_the_same_key_is_told_to_wait()
    {
        _screening.Delay = TimeSpan.FromMilliseconds(600);

        var first = TransferAsync("NB-1001", "NB-1002", 20_000m, key: "key-1");
        await Task.Delay(150);
        var retry = await TransferAsync("NB-1001", "NB-1002", 20_000m, key: "key-1");

        Assert.Equal(LedgerError.RequestInProgress, retry.Error);
        Assert.True((await first).IsSuccess);

        var afterwards = await TransferAsync("NB-1001", "NB-1002", 20_000m, key: "key-1");
        Assert.Equal((await first).Value!.Id, afterwards.Value!.Id);
        Assert.Single(await TransactionsAsync());
    }

    [Fact]
    public async Task A_claim_left_behind_by_a_crashed_request_is_taken_over_after_a_while()
    {
        await InsertUnfinishedClaimAsync("key-1", "NB-1001", "NB-1002", 100m, age: TimeSpan.FromMinutes(10));

        var result = await TransferAsync("NB-1001", "NB-1002", 100m, key: "key-1");

        Assert.True(result.IsSuccess);
        Assert.Single(await TransactionsAsync());
    }

    [Fact]
    public async Task A_fresh_unfinished_claim_is_respected()
    {
        await InsertUnfinishedClaimAsync("key-1", "NB-1001", "NB-1002", 100m, age: TimeSpan.FromSeconds(5));

        var result = await TransferAsync("NB-1001", "NB-1002", 100m, key: "key-1");

        Assert.Equal(LedgerError.RequestInProgress, result.Error);
        await AssertNothingBookedAsync();
    }

    private async Task InsertUnfinishedClaimAsync(string key, string from, string to, decimal amount, TimeSpan age)
    {
        await using var db = NewContext();
        db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Key = key,
            RequestHash = Sut.HashRequest(from, to, Money.Create(amount)),
            CreatedAt = DateTimeOffset.UtcNow - age
        });
        await db.SaveChangesAsync();
    }

    // ---------- Many requests at once ----------

    [Fact]
    public async Task Many_concurrent_requests_with_the_same_key_book_exactly_one_transfer()
    {
        _screening.Delay = TimeSpan.FromMilliseconds(100); // makes the requests overlap

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => TransferAsync("NB-1001", "NB-1002", 15_000m, key: "key-1")));

        var succeeded = results.Where(r => r.IsSuccess).ToList();
        Assert.NotEmpty(succeeded);
        Assert.All(succeeded, r => Assert.Equal(succeeded[0].Value!.Id, r.Value!.Id));
        Assert.All(results.Where(r => !r.IsSuccess), r => Assert.Equal(LedgerError.RequestInProgress, r.Error));

        Assert.Equal(85_000m, await BalanceAsync("NB-1001")); // 15 000 left the account once
        Assert.Single(await TransactionsAsync());
        Assert.Single(await AuditEntriesAsync());
        Assert.Single(_screening.Calls);
        Assert.Equal(OpeningTotal, await TotalAsync());
    }

    [Fact]
    public async Task Concurrent_transfers_never_overdraw_an_account_and_never_create_or_destroy_money()
    {
        // NB-1003 has 50: of 40 transfers of 10 to NB-1002, exactly 5 can succeed.
        var results = await Task.WhenAll(Enumerable.Range(0, 40)
            .Select(i => TransferAsync("NB-1003", "NB-1002", 10m, key: $"key-{i}")));

        Assert.Equal(5, results.Count(r => r.IsSuccess));
        Assert.Equal(35, results.Count(r => r.Error == LedgerError.InsufficientFunds));
        Assert.Equal(0m, await BalanceAsync("NB-1003"));
        Assert.Equal(1_050m, await BalanceAsync("NB-1002"));
        Assert.Equal(OpeningTotal, await TotalAsync());
        Assert.Equal(5, (await TransactionsAsync()).Count);
        Assert.Equal(5, (await AuditEntriesAsync()).Count);
    }

    [Fact]
    public async Task Concurrent_transfers_in_opposite_directions_do_not_deadlock_and_conserve_money()
    {
        var work = Enumerable.Range(0, 80).Select(i => i % 2 == 0
            ? TransferAsync("NB-1001", "NB-1002", 7.25m)
            : TransferAsync("NB-1002", "NB-1001", 3.10m));

        var results = await Task.WhenAll(work);

        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.Equal(OpeningTotal, await TotalAsync());
        Assert.Equal(40 * -7.25m + 40 * 3.10m, await BalanceAsync("NB-1001") - 100_000m);
        Assert.Equal(80, (await TransactionsAsync()).Count);
        Assert.Equal(80, (await AuditEntriesAsync()).Count);
    }

    [Fact]
    public async Task Concurrent_mix_of_deposits_withdrawals_and_transfers_keeps_every_balance_equal_to_the_booked_history()
    {
        var work = Enumerable.Range(0, 90).Select(async i =>
        {
            await using var db = NewContext();
            var ledger = new Sut(db, _screening);
            switch (i % 3)
            {
                case 0: await ledger.DepositAsync("NB-1002", 5m); break;
                case 1: await ledger.WithdrawAsync("NB-1002", 8m); break;
                default: await ledger.TransferAsync("NB-1002", "NB-1003", 6m, $"key-{i}"); break;
            }
        });
        await Task.WhenAll(work);

        var transactions = await TransactionsAsync();
        decimal Expected(string account, decimal opening) => opening + transactions.Sum(t =>
            (t.ToAccount == account ? t.Amount : 0m) - (t.FromAccount == account ? t.Amount : 0m));

        Assert.Equal(Expected("NB-1002", 1_000m), await BalanceAsync("NB-1002"));
        Assert.Equal(Expected("NB-1003", 50m), await BalanceAsync("NB-1003"));
        Assert.Equal(transactions.Count, (await AuditEntriesAsync()).Count);
    }
}
