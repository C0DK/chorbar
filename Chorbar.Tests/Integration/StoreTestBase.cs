using Chorbar.Model;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Chorbar.Tests.Integration;

public abstract class StoreTestBase
{
    protected static readonly TimeSpan TimeStep = TimeSpan.FromMinutes(1);
    private static readonly DateTimeOffset BaseTime = new(2024, 01, 01, 0, 0, 0, TimeSpan.Zero);

    protected static readonly Email UserA = new("alice@example.com");
    protected static readonly Email UserB = new("bob@example.com");

    protected NpgsqlConnection _conn = null!;
    protected FakeTimeProvider _timeProvider = null!;

    protected DateTimeOffset T(int i) => BaseTime.Add(TimeStep * i);

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
