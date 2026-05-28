using Chorbar.Model;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Chorbar.Tests.Integration;

public class UserStoreTest
{
    NpgsqlConnection _conn = null!;

    [SetUp]
    public async Task SetUp()
    {
        _conn = await DatabaseFixture.DataSource.OpenConnectionAsync();
        _timeProvider = new FakeTimeProvider(t(0)) { AutoAdvanceAmount = _timeStep };
        await using var cmd = new NpgsqlCommand("TRUNCATE user_event", _conn);
        await cmd.ExecuteNonQueryAsync();
    }

    [TearDown]
    public async Task TearDown() => await _conn.DisposeAsync();

    [Test, CancelAfter(10_000)]
    public async Task ReadSettingsWithNoEventsReturnsDefaults(CancellationToken cancellationToken)
    {
        var settings = await GetStore(_emailAlice).ReadSettings(cancellationToken);
        Assert.That(
            settings,
            Is.EqualTo(
                new UserSettings(
                    Email: _emailAlice,
                    DisplayName: null,
                    WantReminderEmails: true,
                    History: []
                )
            )
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task ReadInfoWithNoEventsReturnsEmailAsDisplayName(
        CancellationToken cancellationToken
    )
    {
        var info = await GetStore(_emailAlice).ReadInfo(cancellationToken);
        Assert.That(info.DisplayName, Is.EqualTo(_emailAlice.Value));
    }

    [Test, CancelAfter(10_000)]
    public async Task SetDisplayName_Works(CancellationToken cancellationToken)
    {
        var store = GetStore(_emailAlice);
        var settings = await store.Write(new SetDisplayName("Alice"), cancellationToken);

        Assert.That(settings.DisplayName, Is.EqualTo("Alice"));
    }

    [Test, CancelAfter(10_000)]
    public async Task EventIsInHistory(CancellationToken cancellationToken)
    {
        var store = GetStore(_emailAlice);
        var eventPayload = new SetDisplayName("Alice");
        var settings = await store.Write(eventPayload, cancellationToken);

        Assert.That(settings.History.Last().Payload, Is.EqualTo(eventPayload));
    }

    [Test, CancelAfter(10_000)]
    public async Task WriteIncrementsVersionAcrossCalls(CancellationToken cancellationToken)
    {
        var store = GetStore(_emailAlice);
        var settings = await store.Write(
            [new SetDisplayName("A"), new SetDisplayName("B"), new SetDisplayName("C")],
            cancellationToken
        );

        Assert.That(
            settings.History.Select(e => e.Version).ToArray(),
            Is.EqualTo(new[] { 1, 2, 3 })
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task TwoUsersAreIsolated(CancellationToken cancellationToken)
    {
        await GetStore(_emailAlice).Write(new SetDisplayName("Alice"), cancellationToken);

        var bobSettings = await GetStore(_emailBob).ReadSettings(cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(bobSettings.DisplayName, Is.Null);
            Assert.That(bobSettings.History, Is.Empty);
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task ReadInfoReflectsLatestDisplayName(CancellationToken cancellationToken)
    {
        var store = GetStore(_emailAlice);
        await store.Write(
            [new SetDisplayName("Old"), new SetDisplayName("New")],
            cancellationToken
        );

        var info = await store.ReadInfo(cancellationToken);
        Assert.That(info.DisplayName, Is.EqualTo("New"));
    }

    private FakeTimeProvider _timeProvider = null!;

    private UserStore GetStore(Email identity) =>
        new UserStore(_conn, new StaticIdentityProvider(identity), _timeProvider);

    private static readonly TimeSpan _timeStep = TimeSpan.FromMinutes(1);

    private static DateTimeOffset t(int i) =>
        new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero).Add(_timeStep * i);

    private static Email _emailAlice => new Email("alice@example.com");
    private static Email _emailBob => new Email("bob@example.com");
}
