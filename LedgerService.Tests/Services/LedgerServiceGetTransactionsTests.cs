using LedgerService.Server.Data;
using LedgerService.Server.Models.Domain;
using LedgerService.Server.Services;
using LedgerService.Tests.Infrastructure;
using Sut = LedgerService.Server.Services.LedgerService;

namespace LedgerService.Tests.Services;

[Collection(SqlServerCollection.Name)]
public class LedgerServiceGetTransactionsTests(SqlServerFixture sql) : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private Sut _ledger = null!;

    public async Task InitializeAsync()
    {
        _db = SqlServerFixture.CreateContext(await sql.CreateDatabaseAsync());
        _db.Accounts.AddRange(
            Account.Open("NB-1001", "Anna", AccountType.Savings, 1_000m),
            Account.Open("NB-1002", "Erik", AccountType.Savings, 1_000m),
            Account.Open("NB-1003", "Maria", AccountType.Savings, 1_000m));
        await _db.SaveChangesAsync();
        _ledger = new Sut(_db, new StubScreeningClient());
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task Unknown_account_is_not_found()
    {
        var result = await _ledger.GetTransactionsAsync("NB-9999");

        Assert.Equal(LedgerError.AccountNotFound, result.Error);
    }

    [Fact]
    public async Task Account_without_bookings_has_an_empty_list()
    {
        var result = await _ledger.GetTransactionsAsync("NB-1001");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task Lists_deposits_withdrawals_and_transfers_in_both_directions_newest_first()
    {
        await _ledger.DepositAsync("NB-1001", 10m);
        await _ledger.WithdrawAsync("NB-1001", 20m);
        await _ledger.TransferAsync("NB-1001", "NB-1002", 30m);
        await _ledger.TransferAsync("NB-1002", "NB-1001", 40m);

        var list = (await _ledger.GetTransactionsAsync("NB-1001")).Value!;

        Assert.Equal([40m, 30m, 20m, 10m], list.Select(t => t.Amount));
        Assert.Equal(
            [TransactionType.Transfer, TransactionType.Transfer, TransactionType.Withdrawal, TransactionType.Deposit],
            list.Select(t => t.Type));
    }

    [Fact]
    public async Task A_transfer_shows_up_for_both_accounts_and_not_for_others()
    {
        var transfer = (await _ledger.TransferAsync("NB-1001", "NB-1002", 50m)).Value!;

        Assert.Equal(transfer.Id, Assert.Single((await _ledger.GetTransactionsAsync("NB-1001")).Value!).Id);
        Assert.Equal(transfer.Id, Assert.Single((await _ledger.GetTransactionsAsync("NB-1002")).Value!).Id);
        Assert.Empty((await _ledger.GetTransactionsAsync("NB-1003")).Value!);
    }

    [Fact]
    public async Task Only_the_accounts_own_bookings_are_listed()
    {
        await _ledger.DepositAsync("NB-1001", 10m);
        await _ledger.DepositAsync("NB-1002", 99m);

        var list = (await _ledger.GetTransactionsAsync("NB-1001")).Value!;

        Assert.Equal(10m, Assert.Single(list).Amount);
    }

    [Fact]
    public async Task Refused_operations_are_not_listed()
    {
        await _ledger.WithdrawAsync("NB-1001", 5_000m);        // insufficient funds
        await _ledger.TransferAsync("NB-1001", "NB-1002", 0m); // invalid amount

        Assert.Empty((await _ledger.GetTransactionsAsync("NB-1001")).Value!);
    }
}
