using LedgerService.Server.Data;
using LedgerService.Server.Models.Domain;
using LedgerService.Tests.Infrastructure;
using Sut = LedgerService.Server.Services.LedgerService;

namespace LedgerService.Tests.Services;

[Collection(SqlServerCollection.Name)]
public class LedgerServiceGetAccountsTests(SqlServerFixture sql) : IAsyncLifetime
{
    private AppDbContext _db = null!;

    public async Task InitializeAsync() =>
        _db = SqlServerFixture.CreateContext(await sql.CreateDatabaseAsync());

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task Returns_empty_list_when_there_are_no_accounts()
    {
        var accounts = await new Sut(_db, new StubScreeningClient()).GetAccountsAsync();

        Assert.Empty(accounts);
    }

    [Fact]
    public async Task Returns_all_accounts_ordered_by_account_number()
    {
        _db.Accounts.AddRange(
            Account.Open("NB-1003", "Third", AccountType.FixedTerm, 3m),
            Account.Open("NB-1001", "First", AccountType.Savings, 1m),
            Account.Open("NB-1002", "Second", AccountType.Business, 2m));
        await _db.SaveChangesAsync();

        var accounts = await new Sut(_db, new StubScreeningClient()).GetAccountsAsync();

        Assert.Equal(["NB-1001", "NB-1002", "NB-1003"], accounts.Select(a => a.AccountNumber));
    }

    [Fact]
    public async Task Maps_all_fields_and_keeps_exact_balance()
    {
        _db.Accounts.Add(Account.Open("NB-1001", "Anna Lindqvist", AccountType.Business, 12_500.50m));
        await _db.SaveChangesAsync();

        var account = Assert.Single(await new Sut(_db, new StubScreeningClient()).GetAccountsAsync());

        Assert.Equal("NB-1001", account.AccountNumber);
        Assert.Equal("Anna Lindqvist", account.Owner);
        Assert.Equal(AccountType.Business, account.Type);
        Assert.Equal(12_500.50m, account.Balance);
        Assert.Equal("SEK", account.Currency);
    }

    [Fact]
    public async Task Returns_the_seeded_accounts_including_the_rejected_company()
    {
        await DbSeeder.SeedAsync(_db);

        var accounts = await new Sut(_db, new StubScreeningClient()).GetAccountsAsync();

        Assert.Equal(5, accounts.Count);
        Assert.Contains(accounts, a => a.AccountNumber == DbSeeder.RejectedCompanyAccount && a.Type == AccountType.Business);
    }
}
