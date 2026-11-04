using LedgerService.Server.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace LedgerService.Tests.Infrastructure;

/// <summary>
/// One real SQL Server container shared by all tests. Each test gets its own freshly migrated database,
/// so tests are isolated and run against the same engine (constraints, locking) as production.
/// </summary>
public class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Creates a new empty, migrated database and returns its connection string.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var builder = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = $"ledger_test_{Guid.NewGuid():N}"
        };

        await using var db = CreateContext(builder.ConnectionString);
        await db.Database.MigrateAsync();
        return builder.ConnectionString;
    }

    public static AppDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options);
}

[CollectionDefinition(Name)]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
