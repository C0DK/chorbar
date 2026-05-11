using Chorbar.Model;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Chorbar.Tests;

public class IcalTest
{
    NpgsqlConnection _conn = null!;
    FakeTimeProvider _timeProvider = null!;
    static Email _userA => new Email("alice@example.com");
    static TimeSpan _timeStep = TimeSpan.FromMinutes(1);

    DateTimeOffset t(int i) =>
        new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero).Add(_timeStep * i);

    [SetUp]
    public async Task SetUp()
    {
        _conn = await DatabaseFixture.DataSource.OpenConnectionAsync();
        _timeProvider = new FakeTimeProvider(t(0)) { AutoAdvanceAmount = _timeStep };
        await using var cmd = new NpgsqlCommand("TRUNCATE household_event", _conn);
        await cmd.ExecuteNonQueryAsync();
    }

    [TearDown]
    public async Task TearDown() => await _conn.DisposeAsync();

    HouseholdStore GetStore() =>
        new HouseholdStore(_conn, new StaticIdentityProvider(_userA), _timeProvider);

    [Test, CancelAfter(10_000)]
    public async Task GenerateIcalTokenIsStoredInHousehold(CancellationToken ct)
    {
        var store = GetStore();
        var id = await store.New("My House", ct);
        var token = Guid.NewGuid().ToString("N");

        var household = await store.Write(id, new GenerateIcalToken(token), ct);

        Assert.That(household.IcalToken, Is.EqualTo(token));
    }

    [Test, CancelAfter(10_000)]
    public async Task ReadForIcalReturnsHouseholdWithCorrectToken(CancellationToken ct)
    {
        var store = GetStore();
        var id = await store.New("My House", ct);
        var token = Guid.NewGuid().ToString("N");
        await store.Write(id, new GenerateIcalToken(token), ct);

        var household = await store.ReadForIcal(id, token, ct);

        Assert.That(household, Is.Not.Null);
        Assert.That(household!.Name, Is.EqualTo("My House"));
    }

    [Test, CancelAfter(10_000)]
    public async Task ReadForIcalReturnsNullWithWrongToken(CancellationToken ct)
    {
        var store = GetStore();
        var id = await store.New("My House", ct);
        var token = Guid.NewGuid().ToString("N");
        await store.Write(id, new GenerateIcalToken(token), ct);

        var household = await store.ReadForIcal(id, "wrongtoken", ct);

        Assert.That(household, Is.Null);
    }

    [Test, CancelAfter(10_000)]
    public async Task ReadForIcalReturnsNullForNonExistentHousehold(CancellationToken ct)
    {
        var store = GetStore();
        var household = await store.ReadForIcal(new HouseholdId(99999), "anytoken", ct);

        Assert.That(household, Is.Null);
    }

    [Test, CancelAfter(10_000)]
    public async Task ReadForIcalReturnsNullWhenNoTokenGenerated(CancellationToken ct)
    {
        var store = GetStore();
        var id = await store.New("My House", ct);

        var household = await store.ReadForIcal(id, "anytoken", ct);

        Assert.That(household, Is.Null);
    }

    [Test, CancelAfter(10_000)]
    public async Task RegeneratingTokenOverridesPreviousToken(CancellationToken ct)
    {
        var store = GetStore();
        var id = await store.New("My House", ct);
        var oldToken = Guid.NewGuid().ToString("N");
        var newToken = Guid.NewGuid().ToString("N");

        await store.Write(id, new GenerateIcalToken(oldToken), ct);
        await store.Write(id, new GenerateIcalToken(newToken), ct);

        Assert.That(await store.ReadForIcal(id, oldToken, ct), Is.Null);
        Assert.That(await store.ReadForIcal(id, newToken, ct), Is.Not.Null);
    }
}
