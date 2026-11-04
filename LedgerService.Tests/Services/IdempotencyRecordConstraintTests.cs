using LedgerService.Server.Data;
using LedgerService.Server.Models.Domain;
using LedgerService.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Sut = LedgerService.Server.Services.LedgerService;

namespace LedgerService.Tests.Services;

[Collection(SqlServerCollection.Name)]
public class IdempotencyRecordConstraintTests(SqlServerFixture sql) : IAsyncLifetime
{
    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _db = SqlServerFixture.CreateContext(await sql.CreateDatabaseAsync());
        _db.Accounts.AddRange(
            Account.Open("NB-1001", "Anna", AccountType.Savings, 1_000m),
            Account.Open("NB-1002", "Erik", AccountType.Savings));
        await _db.SaveChangesAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task Two_idempotency_records_cannot_point_at_the_same_transaction()
    {
        var booked = (await new Sut(_db, new StubScreeningClient()).TransferAsync("NB-1001", "NB-1002", 10m, "key-1")).Value!;

        _db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Key = "key-2",
            RequestHash = "x",
            CompletedAt = DateTimeOffset.UtcNow,
            ResultTransactionId = booked.Id
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task Many_unfinished_records_without_a_result_are_allowed()
    {
        _db.IdempotencyRecords.AddRange(
            new IdempotencyRecord { Key = "a", RequestHash = "x" },
            new IdempotencyRecord { Key = "b", RequestHash = "x" });

        await _db.SaveChangesAsync();

        Assert.Equal(2, await _db.IdempotencyRecords.CountAsync());
    }
}
