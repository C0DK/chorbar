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
    public async Task ReadSettingsWithNoEventsReturnsDefaults(
        CancellationToken cancellationToken
    )
    {
        var settings = await GetStore(_userA).ReadSettings(cancellationToken);
        Assert.That(
            settings,
            Is.EqualTo(
                new UserSettings(
                    Email: _userA,
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
        var info = await GetStore(_userA).ReadInfo(cancellationToken);
        Assert.That(info, Is.EqualTo(new UserInfo(Email: _userA, DisplayName: _userA)));
    }

    [Test, CancelAfter(10_000)]
    public async Task WritePersistsEvent(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);
        var settings = await store.Write(new SetDisplayName("Alice"), cancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(settings.DisplayName, Is.EqualTo("Alice"));
            Assert.That(settings.History, Has.Length.EqualTo(1));
            Assert.That(settings.History[0].Version, Is.EqualTo(1));
            Assert.That(settings.History[0].Payload, Is.EqualTo(new SetDisplayName("Alice")));
            Assert.That(settings.History[0].Email, Is.EqualTo(_userA));
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task WriteIncrementsVersionAcrossCalls(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);
        await store.Write(new SetDisplayName("A"), cancellationToken);
        await store.Write(new SetDisplayName("B"), cancellationToken);
        var settings = await store.Write(new SetDisplayName("C"), cancellationToken);

        Assert.That(
            settings.History.Select(e => e.Version).ToArray(),
            Is.EqualTo(new[] { 1, 2, 3 })
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task WriteBatchIncrementsVersionWithinBatch(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);
        var settings = await store.Write(
            [new SetDisplayName("A"), new SetDisplayName("B"), new SetDisplayName("C")],
            cancellationToken
        );

        Assert.Multiple(() =>
        {
            Assert.That(
                settings.History.Select(e => e.Version).ToArray(),
                Is.EqualTo(new[] { 1, 2, 3 })
            );
            Assert.That(settings.DisplayName, Is.EqualTo("C"));
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task HistoryOrderedByTimestamp(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);
        await store.Write(
            [new SetDisplayName("First"), new SetDisplayName("Second")],
            cancellationToken
        );

        var settings = await store.ReadSettings(cancellationToken);
        Assert.That(
            settings.History.Select(e => e.Timestamp).ToArray(),
            Is.Ordered.Ascending
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task TwoUsersAreIsolated(CancellationToken cancellationToken)
    {
        await GetStore(_userA).Write(new SetDisplayName("Alice"), cancellationToken);

        var bobSettings = await GetStore(_userB).ReadSettings(cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(bobSettings.DisplayName, Is.Null);
            Assert.That(bobSettings.History, Is.Empty);
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task SecondUserCanWriteIndependently(CancellationToken cancellationToken)
    {
        await GetStore(_userA).Write(new SetDisplayName("Alice"), cancellationToken);
        await GetStore(_userB).Write(new SetDisplayName("Bob"), cancellationToken);

        var alice = await GetStore(_userA).ReadSettings(cancellationToken);
        var bob = await GetStore(_userB).ReadSettings(cancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(alice.DisplayName, Is.EqualTo("Alice"));
            Assert.That(bob.DisplayName, Is.EqualTo("Bob"));
            Assert.That(alice.History, Has.Length.EqualTo(1));
            Assert.That(bob.History, Has.Length.EqualTo(1));
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task PayloadRoundtripsThroughJsonb(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);
        await store.Write(
            [new SetDisplayName("Alice"), new SetWantEmailReminders(false)],
            cancellationToken
        );

        var settings = await store.ReadSettings(cancellationToken);
        Assert.That(
            settings.History.Select(e => e.Payload).ToArray(),
            Is.EqualTo(
                new UserEventPayload[]
                {
                    new SetDisplayName("Alice"),
                    new SetWantEmailReminders(false),
                }
            )
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task ReadInfoReflectsLatestDisplayName(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);
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

    private static Email _userA => new Email("alice@example.com");
    private static Email _userB => new Email("bob@example.com");
}
