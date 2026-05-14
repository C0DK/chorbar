using Chorbar.Model;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Chorbar.Tests;

public abstract class StoreTestBase : TimeTestBase
{
    protected NpgsqlConnection _conn = null!;
    protected FakeTimeProvider _timeProvider = null!;

    protected static readonly Email UserA = new("alice@example.com");
    protected static readonly Email UserB = new("bob@example.com");

    protected HouseholdStore GetStore(Email? identity = null) =>
        new(_conn, new StaticIdentityProvider(identity ?? UserA), _timeProvider);

    [SetUp]
    public async Task BaseSetUp()
    {
        _conn = await DatabaseFixture.DataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("TRUNCATE household_event", _conn);
        await cmd.ExecuteNonQueryAsync();
    }

    [TearDown]
    public async Task BaseTearDown() => await _conn.DisposeAsync();
}
