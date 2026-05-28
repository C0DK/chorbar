using Chorbar.Const;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Chorbar.Tests.Integration;

[SetUpFixture]
public class DatabaseFixture
{
    static PostgreSqlContainer? _container;
    static NpgsqlDataSource? _dataSource;
    public static NpgsqlDataSource DataSource => _dataSource ?? throw new NullReferenceException();

    internal static string ConnectionString =>
        _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Container not started");

    [OneTimeSetUp]
    public async Task StartContainer()
    {
        _container = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await _container.StartAsync();
        await using var conn = new NpgsqlConnection(DatabaseFixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(Sql.init, conn);
        await cmd.ExecuteNonQueryAsync();
        _dataSource = NpgsqlDataSource.Create(ConnectionString);
    }

    [OneTimeTearDown]
    public async Task StopContainer()
    {
        if (_container is not null)
            await _container.DisposeAsync();
        if (_dataSource is not null)
            await _dataSource.DisposeAsync();
    }
}
