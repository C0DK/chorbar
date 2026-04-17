using Chorbar.Model;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Chorbar.Tests;

public class UserStoreTest
{
    NpgsqlConnection _conn = null!;

    [SetUp]
    public async Task SetUp()
    {
        _conn = await DatabaseFixture.DataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("TRUNCATE user_event", _conn);

        _timeProvider = new FakeTimeProvider(t(0)) { AutoAdvanceAmount = _timeStep };
        await cmd.ExecuteNonQueryAsync();
    }

    [TearDown]
    public async Task TearDown() => await _conn.DisposeAsync();

    [Test, CancelAfter(10_000)]
    public async Task PlainGetReturnsEmptyUser(CancellationToken cancellationToken)
    {
        var store = GetSubject();

        var user = await store.Read(_identity, cancellationToken);
        Assert.That(user, Is.EqualTo(new User(_identity, [], [])));
    }

    [Test, CancelAfter(10_000)]
    public async Task HasChoreAfterAdd(CancellationToken cancellationToken)
    {
        var store = GetSubject();

        var user = await store.Write(_identity, new AddChore("Sleep"), cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(user.Chores.Keys, Is.EquivalentTo(["Sleep"]));
            Assert.That(
                user.History,
                Is.EquivalentTo([new UserEvent(_identity, 1, t(0), new AddChore("Sleep"))])
            );
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task HasNameAfterRename(CancellationToken cancellationToken)
    {
        var store = GetSubject();

        await store.Write(_identity, new AddChore("Sleep"), cancellationToken);
        var user = await store.Write(
            _identity,
            new RenameChore("Sleep", "Sleep sound"),
            cancellationToken
        );
        Assert.Multiple(() =>
        {
            Assert.That(user.Chores.Keys, Is.EquivalentTo(["Sleep sound"]));
            Assert.That(
                user.History,
                Is.EquivalentTo(
                    [
                        new UserEvent(_identity, 1, t(0), new AddChore("Sleep")),
                        new UserEvent(_identity, 2, t(1), new RenameChore("Sleep", "Sleep sound")),
                    ]
                )
            );
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task DoChoreDoesIt(CancellationToken cancellationToken)
    {
        var store = GetSubject();
        await store.Write(_identity, new AddChore("Sleep"), cancellationToken);
        var user = await store.Write(_identity, new DoChore("Sleep"), cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(user.Chores.Keys, Is.EquivalentTo(["Sleep"]));
            Assert.That(user.Chores["Sleep"], Is.EqualTo(new Chore(t(0), [t(1)])));
            Assert.That(
                user.History,
                Is.EquivalentTo(
                    [
                        new UserEvent(_identity, 1, t(0), new AddChore("Sleep")),
                        new UserEvent(_identity, 2, t(1), new DoChore("Sleep")),
                    ]
                )
            );
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task AddTwoChoresAndDoBothDoesIt(CancellationToken cancellationToken)
    {
        var store = GetSubject();
        await store.Write(
            _identity,
            [
                new AddChore("A"),
                new DoChore("A"),
                new AddChore("B"),
                new DoChore("A"),
                new DoChore("B"),
            ],
            cancellationToken
        );

        var user = await store.Read(_identity, cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(user.Chores.Keys, Is.EquivalentTo(["A", "B"]));
            Assert.That(user.Chores["A"], Is.EqualTo(new Chore(t(0), [t(1), t(3)])));
            Assert.That(user.Chores["B"], Is.EqualTo(new Chore(t(2), [t(4)])));
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task SetGoal(CancellationToken cancellationToken)
    {
        var store = GetSubject();
        await store.Write(
            _identity,
            [new AddChore("A"), new SetGoal("A", TimeSpan.FromMinutes(2)), new DoChore("A")],
            cancellationToken
        );

        var user = await store.Read(_identity, cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(user.Chores.Keys, Is.EquivalentTo(["A"]));
            Assert.That(
                user.Chores["A"],
                Is.EqualTo(new Chore(t(0), [t(2)], idealFrequency: TimeSpan.FromMinutes(2)))
            );
            Assert.That(
                user.History,
                Is.EquivalentTo(
                    [
                        new UserEvent(_identity, 1, t(0), new AddChore("A")),
                        new UserEvent(
                            _identity,
                            2,
                            t(1),
                            new SetGoal("A", TimeSpan.FromMinutes(2))
                        ),
                        new UserEvent(_identity, 3, t(2), new DoChore("A")),
                    ]
                )
            );
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task TwoUsersDoesNotImpactEachother(CancellationToken cancellationToken)
    {
        var store = GetSubject();
        await store.Write(_identity, [new AddChore("A"), new DoChore("A")], cancellationToken);
        await store.Write(
            _otherIdentity,
            [new AddChore("A"), new DoChore("A"), new AddChore("B")],
            cancellationToken
        );

        var user = await store.Read(_identity, cancellationToken);
        var otherUser = await store.Read(_otherIdentity, cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(user.Chores.Keys, Is.EquivalentTo(["A"]));
            Assert.That(otherUser.Chores.Keys, Is.EquivalentTo(["A", "B"]));
            Assert.That(user.Chores["A"], Is.EqualTo(new Chore(t(0), [t(1)])));
            Assert.That(otherUser.Chores["A"], Is.EqualTo(new Chore(t(2), [t(3)])));
            Assert.That(otherUser.Chores["B"], Is.EqualTo(new Chore(t(4), [])));
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task RenameNonExistingThrows(CancellationToken cancellationToken)
    {
        var store = GetSubject();

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(_identity, new RenameChore("Sleep", "Sleep sound"), cancellationToken)
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task DoNonExistingActivityThrows(CancellationToken cancellationToken)
    {
        var store = new UserStore(_conn);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(_identity, new DoChore("Sleep"), cancellationToken)
        );
    }

    private static TimeSpan _timeStep = TimeSpan.FromMinutes(1);

    private DateTimeOffset t(int i) =>
        new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero).Add(_timeStep * i);

    private FakeTimeProvider _timeProvider = null!;

    private UserStore GetSubject() => new UserStore(_conn, _timeProvider);

    private static Email _identity => new Email("test@example.org");
    private static Email _otherIdentity => new Email("test2@example.org");
}
