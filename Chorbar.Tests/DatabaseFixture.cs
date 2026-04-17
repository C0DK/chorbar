using Chorbar.Const;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Chorbar.Tests;

[SetUpFixture]
public class DatabaseFixture
{
    // Note: stikl.chat_event has a missing comma in the production SQL file
    // (between `kind TEXT NOT NULL` and `payload TEXT NOT NULL`). The corrected
    // version is used here.
    static PostgreSqlContainer? _container;
    public static NpgsqlDataSource DataSource => NpgsqlDataSource.Create(ConnectionString);

    internal static string ConnectionString =>
        _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Container not started");

    [OneTimeSetUp]
    public async Task StartContainer()
    {
        _container = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();

        await _container.StartAsync();
        var conn = new NpgsqlConnection(DatabaseFixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(Sql.create_event_table, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    [OneTimeTearDown]
    public async Task StopContainer()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
