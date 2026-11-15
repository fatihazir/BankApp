using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Persistence.Contexts;
using Persistence.EntityConfigurations;
using Respawn;
using Respawn.Graph;
using Testcontainers.MsSql;

namespace Persistence.IntegrationTests.Infrastructure;

/// <summary>
/// Starts one throwaway SQL Server container for the whole test run, creates the schema once,
/// and resets the data (Respawn) before every test.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private Respawner? _respawner;

    /// <summary>Connection string of a dedicated test database (not the server's master database).</summary>
    public string ConnectionString => new SqlConnectionStringBuilder(_container.GetConnectionString())
    {
        InitialCatalog = "BankAppTests"
    }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<BankAppDbContext>().UseSqlServer(ConnectionString).Options;
        await using (var context = new BankAppDbContext(options))
        {
            await context.Database.MigrateAsync();
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = ["dbo"],
            // Keep the migration history, otherwise the schema would be created a second time.
            TablesToIgnore = [new Table("__EFMigrationsHistory")]
        });
    }

    public async Task ResetAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);

        // Loan types are reference data inserted by the migration. Respawn removed them with everything else,
        // so put them back; every test starts from the same state, even if it created or changed loan types.
        var options = new DbContextOptionsBuilder<BankAppDbContext>().UseSqlServer(ConnectionString).Options;
        await using var context = new BankAppDbContext(options);
        context.LoanTypes.AddRange(LoanSeedData.LoanTypes());
        context.LoanSubTypes.AddRange(LoanSeedData.LoanSubTypes());
        await context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "Database";
}
