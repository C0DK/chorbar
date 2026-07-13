using Chorbar.Model;
using Chorbar.Utils;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Chorbar.Tests.Integration;

public class SetWantEmailRemindersTest
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
    public async Task DefaultIsTrue(CancellationToken cancellationToken)
    {
        var settings = await GetStore(_userA).ReadSettings(cancellationToken);
        Assert.That(settings.WantReminderEmails, Is.True);
    }

    [Test, CancelAfter(10_000)]
    public async Task SetFalseTurnsItOff(CancellationToken cancellationToken)
    {
        var settings = await GetStore(_userA)
            .Write(new SetWantEmailReminders(false), cancellationToken);

        Assert.That(settings.WantReminderEmails, Is.False);
    }

    [Test, CancelAfter(10_000)]
    public async Task TogglesBackOn(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);
        await store.Write(new SetWantEmailReminders(false), cancellationToken);
        var settings = await store.Write(new SetWantEmailReminders(true), cancellationToken);

        Assert.That(settings.WantReminderEmails, Is.True);
    }

    [Test, CancelAfter(10_000)]
    public async Task DoesNotAffectDisplayName(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);
        await store.Write(new SetDisplayName("Alice"), cancellationToken);
        var settings = await store.Write(new SetWantEmailReminders(false), cancellationToken);

        Assert.That(settings.DisplayName, Is.EqualTo("Alice"));
    }

    [Test, CancelAfter(10_000)]
    public async Task DoesNotChangeUserInfo(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);
        await store.Write(new SetDisplayName("Alice"), cancellationToken);
        await store.Write(new SetWantEmailReminders(false), cancellationToken);

        var info = await store.ReadInfo(cancellationToken);
        Assert.That(info, Is.EqualTo(new UserInfo(_userA, "Alice")));
    }

    [Test]
    public void IsValidAlwaysTrue()
    {
        var settings = UserSettings.Create(_userA);
        Assert.Multiple(() =>
        {
            Assert.That(new SetWantEmailReminders(true).IsValid(settings), Is.True);
            Assert.That(new SetWantEmailReminders(false).IsValid(settings), Is.True);
        });
    }

    [Test]
    public void ApplyOnSettingsChangesOnlyFlag()
    {
        var settings = UserSettings.Create(_userA) with { DisplayName = "Alice" };
        var updated = new SetWantEmailReminders(false).Apply(settings, t(0));

        Assert.Multiple(() =>
        {
            Assert.That(updated.WantReminderEmails, Is.False);
            Assert.That(updated.DisplayName, Is.EqualTo("Alice"));
            Assert.That(updated.Email, Is.EqualTo(_userA));
        });
    }

    [Test]
    public void ApplyOnInfoIsNoOp()
    {
        var info = UserInfo.Create(_userA) with { DisplayName = "Alice" };
        var updated = new SetWantEmailReminders(false).Apply(info, t(0));

        Assert.That(updated, Is.EqualTo(info));
    }

    [Test]
    public void SerializesAndDeserializes()
    {
        UserEventPayload original = new SetWantEmailReminders(false);
        var roundtripped = UserEventPayload.Deserialize(original.Serialize());

        Assert.That(roundtripped, Is.EqualTo(original));
    }

    [Test]
    public void EventKindIsStable()
    {
        Assert.That(
            new SetWantEmailReminders(true).EventKind,
            Is.EqualTo("set_want_email_reminders")
        );
    }

    private FakeTimeProvider _timeProvider = null!;

    private UserStore GetStore(Email identity) =>
        new UserStore(
            _conn,
            new StaticIdentityProvider(identity),
            _timeProvider,
            new AsyncLazyCache<Email, UserInfo>()
        );

    private static readonly TimeSpan _timeStep = TimeSpan.FromMinutes(1);

    private static DateTimeOffset t(int i) =>
        new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero).Add(_timeStep * i);

    private static Email _userA => new Email("alice@example.com");
}
