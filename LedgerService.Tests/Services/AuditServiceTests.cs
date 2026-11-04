using LedgerService.Server.Data;
using LedgerService.Server.Models.Domain;
using LedgerService.Server.Services;
using LedgerService.Tests.Infrastructure;
using Sut = LedgerService.Server.Services.LedgerService;

namespace LedgerService.Tests.Services;

[Collection(SqlServerCollection.Name)]
public class AuditServiceTests(SqlServerFixture sql) : IAsyncLifetime
{
    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _db = SqlServerFixture.CreateContext(await sql.CreateDatabaseAsync());
        _db.Accounts.AddRange(
            Account.Open("NB-1001", "Anna", AccountType.Savings),
            Account.Open("NB-1002", "Erik", AccountType.Savings));
        await _db.SaveChangesAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task Returns_empty_trail_for_account_without_bookings()
    {
        Assert.Empty(await new AuditService(_db).GetAuditTrailAsync("NB-1001"));
    }

    [Fact]
    public async Task Returns_only_the_accounts_own_entries_oldest_first()
    {
        var ledger = new Sut(_db);
        await ledger.DepositAsync("NB-1001", 10m);
        await ledger.DepositAsync("NB-1002", 99m);
        await ledger.DepositAsync("NB-1001", 20m);

        var trail = await new AuditService(_db).GetAuditTrailAsync("NB-1001");

        Assert.Equal([10m, 20m], trail.Select(a => a.Amount));
        Assert.All(trail, a => Assert.Equal("NB-1001", a.ToAccount));
    }

    [Fact]
    public async Task Includes_entries_where_the_account_is_the_sending_side()
    {
        // Transfers are not implemented yet, so a transfer-shaped booking is inserted directly.
        var transfer = new Transaction
        {
            Type = TransactionType.Transfer, FromAccount = "NB-1001", ToAccount = "NB-1002", Amount = 5m
        };
        _db.Transactions.Add(transfer);
        _db.AuditEntries.Add(AuditEntry.From(transfer));
        await _db.SaveChangesAsync();

        var service = new AuditService(_db);

        Assert.Single(await service.GetAuditTrailAsync("NB-1001"));
        Assert.Single(await service.GetAuditTrailAsync("NB-1002"));
    }
}
