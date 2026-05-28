using Chorbar.Model;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Chorbar.Tests.Integration;

public class SetDisplayNameTest
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
    public async Task SetsDisplayName(CancellationToken cancellationToken)
    {
        var settings = await GetStore(_userA)
            .Write(new SetDisplayName("Alice"), cancellationToken);

        Assert.That(settings.DisplayName, Is.EqualTo("Alice"));
    }

    [Test, CancelAfter(10_000)]
    public async Task LastWriteWins(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);
        await store.Write(new SetDisplayName("First"), cancellationToken);
        var settings = await store.Write(new SetDisplayName("Second"), cancellationToken);

        Assert.That(settings.DisplayName, Is.EqualTo("Second"));
    }

    [Test, CancelAfter(10_000)]
    public async Task ReflectedInReadInfo(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);
        await store.Write(new SetDisplayName("Alice"), cancellationToken);

        var info = await store.ReadInfo(cancellationToken);
        Assert.That(info.DisplayName, Is.EqualTo("Alice"));
    }

    [Test, CancelAfter(10_000)]
    public async Task DoesNotAffectWantReminderEmails(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);
        await store.Write(new SetWantEmailReminders(false), cancellationToken);
        var settings = await store.Write(new SetDisplayName("Alice"), cancellationToken);

        Assert.That(settings.WantReminderEmails, Is.False);
    }

    [Test]
    public void IsValidAlwaysTrue()
    {
        var settings = UserSettings.Create(_userA);
        Assert.That(new SetDisplayName("Anything").IsValid(settings), Is.True);
    }

    [Test]
    public void IsValidTrueWhenAlreadySet()
    {
        var settings = UserSettings.Create(_userA) with { DisplayName = "Existing" };
        Assert.That(new SetDisplayName("Different").IsValid(settings), Is.True);
    }

    [Test]
    public void ApplyOnSettingsChangesOnlyDisplayName()
    {
        var settings = UserSettings.Create(_userA) with { WantReminderEmails = false };
        var updated = new SetDisplayName("Alice").Apply(settings, t(0));

        Assert.Multiple(() =>
        {
            Assert.That(updated.DisplayName, Is.EqualTo("Alice"));
            Assert.That(updated.WantReminderEmails, Is.False);
            Assert.That(updated.Email, Is.EqualTo(_userA));
        });
    }

    [Test]
    public void ApplyOnInfoChangesDisplayName()
    {
        var info = UserInfo.Create(_userA);
        var updated = new SetDisplayName("Alice").Apply(info, t(0));

        Assert.Multiple(() =>
        {
            Assert.That(updated.DisplayName, Is.EqualTo("Alice"));
            Assert.That(updated.Email, Is.EqualTo(_userA));
        });
    }

    [Test]
    public void SerializesAndDeserializes()
    {
        UserEventPayload original = new SetDisplayName("Alice");
        var roundtripped = UserEventPayload.Deserialize(original.Serialize());

        Assert.That(roundtripped, Is.EqualTo(original));
    }

    [Test]
    public void EventKindIsStable()
    {
        Assert.That(new SetDisplayName("x").EventKind, Is.EqualTo("set_display_name"));
    }

    private FakeTimeProvider _timeProvider = null!;

    private UserStore GetStore(Email identity) =>
        new UserStore(_conn, new StaticIdentityProvider(identity), _timeProvider);

    private static readonly TimeSpan _timeStep = TimeSpan.FromMinutes(1);

    private static DateTimeOffset t(int i) =>
        new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero).Add(_timeStep * i);

    private static Email _userA => new Email("alice@example.com");
}
