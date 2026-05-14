using System.Collections.Immutable;
using Chorbar.Model;
using Microsoft.Extensions.Time.Testing;

namespace Chorbar.Tests;

public class ChoreStoreTests : StoreTestBase
{
    private HouseholdId _householdAId;
    private HouseholdId _householdBId;
    private ImmutableArray<HouseholdEvent> _householdAInitialEvents;

    [SetUp]
    public async Task SetUp()
    {
        _timeProvider = new FakeTimeProvider(T(-4)) { AutoAdvanceAmount = TimeStep };
        var store = GetStore(UserA);
        _householdAId = await store.New("Some Name", CancellationToken.None);
        _householdAInitialEvents = (
            await store.Write(_householdAId, new AddMember(UserB), CancellationToken.None)
        ).History;
        _householdBId = await store.New("Some Other Name", CancellationToken.None);
        await store.Write(_householdBId, new AddMember(UserB), CancellationToken.None);
    }

    [Test, CancelAfter(10_000)]
    public async Task HasChoreAfterAdd(CancellationToken cancellationToken)
    {
        var store = GetStore();
        var household = await store.Write(_householdAId, new AddChore("Sleep"), cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(household.Chores.Keys, Is.EquivalentTo(["Sleep"]));
            Assert.That(
                household.History,
                Is.EquivalentTo(
                    [
                        .. _householdAInitialEvents,
                        new HouseholdEvent(_householdAId, 3, T(0), new AddChore("Sleep"), UserA),
                    ]
                )
            );
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task HasNameAfterRename(CancellationToken cancellationToken)
    {
        var store = GetStore();
        await store.Write(
            _householdAId,
            [new AddChore("Sleep"), new RenameChore("Sleep", "Sleep sound")],
            cancellationToken
        );
        var household = await store.Read(_householdAId, cancellationToken);
        Assert.That(household.Chores.Keys, Is.EquivalentTo(["Sleep sound"]));
    }

    [Test, CancelAfter(10_000)]
    public async Task DoChoreDoesIt(CancellationToken cancellationToken)
    {
        var store = GetStore();
        await store.Write(
            _householdAId,
            [new AddChore("Sleep"), new DoChore("Sleep")],
            cancellationToken
        );
        var household = await store.Read(_householdAId, cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(household.Chores.Keys, Is.EquivalentTo(["Sleep"]));
            Assert.That(household.Chores["Sleep"], Is.EqualTo(new Chore(T(0), [T(1)])));
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task UndoChoreUndoesIt(CancellationToken cancellationToken)
    {
        var store = GetStore();
        await store.Write(
            _householdAId,
            [
                new AddChore("Sleep"),
                new DoChore("Sleep"),
                new DoChore("Sleep"),
                new UndoChore("Sleep", T(1)),
            ],
            cancellationToken
        );
        var household = await store.Read(_householdAId, cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(household.Chores.Keys, Is.EquivalentTo(["Sleep"]));
            Assert.That(household.Chores["Sleep"], Is.EqualTo(new Chore(T(0), [T(2)])));
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task AddTwoChoresAndDoBothDoesIt(CancellationToken cancellationToken)
    {
        var store = GetStore();
        await store.Write(
            _householdAId,
            [
                new AddChore("A"),
                new DoChore("A"),
                new AddChore("B"),
                new DoChore("A"),
                new DoChore("B"),
            ],
            cancellationToken
        );
        var household = await store.Read(_householdAId, cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(household.Chores.Keys, Is.EquivalentTo(["A", "B"]));
            Assert.That(household.Chores["A"], Is.EqualTo(new Chore(T(0), [T(1), T(3)])));
            Assert.That(household.Chores["B"], Is.EqualTo(new Chore(T(2), [T(4)])));
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task SetGoal(CancellationToken cancellationToken)
    {
        var store = GetStore();
        await store.Write(
            _householdAId,
            [new AddChore("A"), new SetGoal("A", 2, DateUnit.Day), new DoChore("A")],
            cancellationToken
        );
        var household = await store.Read(_householdAId, cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(household.Chores.Keys, Is.EquivalentTo(["A"]));
            Assert.That(
                household.Chores["A"],
                Is.EqualTo(new Chore(T(0), [T(2)], Goal: new Goal(2, DateUnit.Day)))
            );
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task ChoreHasNoGoalByDefault(CancellationToken cancellationToken)
    {
        var store = GetStore();
        await store.Write(_householdAId, new AddChore("Sleep"), cancellationToken);
        var household = await store.Read(_householdAId, cancellationToken);
        Assert.That(household.Chores["Sleep"].Goal, Is.Null);
    }

    [Test, CancelAfter(10_000)]
    public async Task OverrideGoalReplacesFrequency(CancellationToken cancellationToken)
    {
        var store = GetStore();
        await store.Write(
            _householdAId,
            [
                new AddChore("Sleep"),
                new SetGoal("Sleep", 2, DateUnit.Day),
                new SetGoal("Sleep", 5, DateUnit.Week),
            ],
            cancellationToken
        );
        var household = await store.Read(_householdAId, cancellationToken);
        Assert.That(household.Chores["Sleep"].Goal, Is.EqualTo(new Goal(5, DateUnit.Week)));
    }

    [Test, CancelAfter(10_000)]
    public async Task SetGoalOnNonExistingChoreThrows(CancellationToken cancellationToken)
    {
        var store = GetStore();
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(
                _householdAId,
                new SetGoal("Sleep", 2, DateUnit.Day),
                cancellationToken
            )
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task TwoHouseholdsDoNotImpactEachOther(CancellationToken cancellationToken)
    {
        var storeA = GetStore(UserA);
        var storeB = GetStore(UserB);
        await storeA.Write(
            _householdAId,
            [new AddChore("A"), new DoChore("A")],
            cancellationToken
        );
        await storeB.Write(
            _householdBId,
            [new AddChore("A"), new DoChore("A"), new AddChore("B")],
            cancellationToken
        );

        var householdA = await storeA.Read(_householdAId, cancellationToken);
        var householdB = await storeB.Read(_householdBId, cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(householdA.Chores.Keys, Is.EquivalentTo(["A"]));
            Assert.That(householdB.Chores.Keys, Is.EquivalentTo(["A", "B"]));
            Assert.That(householdA.Chores["A"], Is.EqualTo(new Chore(T(0), [T(1)])));
            Assert.That(householdB.Chores["A"], Is.EqualTo(new Chore(T(2), [T(3)])));
            Assert.That(householdB.Chores["B"], Is.EqualTo(new Chore(T(4), [])));
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task RenameNonExistingThrows(CancellationToken cancellationToken)
    {
        var store = GetStore();
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(
                _householdAId,
                new RenameChore("Sleep", "Sleep sound"),
                cancellationToken
            )
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task DoNonExistingChoreThrows(CancellationToken cancellationToken)
    {
        var store = GetStore();
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(_householdAId, new DoChore("Sleep"), cancellationToken)
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task AddDuplicateChoreThrows(CancellationToken cancellationToken)
    {
        var store = GetStore();
        await store.Write(_householdAId, new AddChore("Sleep"), cancellationToken);
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(_householdAId, new AddChore("Sleep"), cancellationToken)
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task RenameChoreToExistingLabelThrows(CancellationToken cancellationToken)
    {
        var store = GetStore();
        await store.Write(_householdAId, [new AddChore("A"), new AddChore("B")], cancellationToken);
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(_householdAId, new RenameChore("A", "B"), cancellationToken)
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task RenameChorePreservesHistory(CancellationToken cancellationToken)
    {
        var store = GetStore();
        await store.Write(
            _householdAId,
            [new AddChore("Sleep"), new DoChore("Sleep"), new RenameChore("Sleep", "Rest")],
            cancellationToken
        );
        var household = await store.Read(_householdAId, cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(household.Chores.Keys, Is.EquivalentTo(["Rest"]));
            Assert.That(household.Chores["Rest"].History, Has.Length.EqualTo(1));
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task UndoChoreWithNonExistingTimestampThrows(CancellationToken cancellationToken)
    {
        var store = GetStore();
        await store.Write(_householdAId, new AddChore("Sleep"), cancellationToken);
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(
                _householdAId,
                new UndoChore("Sleep", DateTimeOffset.MinValue),
                cancellationToken
            )
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task RemoveChoreRemovesIt(CancellationToken cancellationToken)
    {
        var store = GetStore();
        await store.Write(_householdAId, new AddChore("Sleep"), cancellationToken);
        var household = await store.Write(
            _householdAId,
            new RemoveChore("Sleep"),
            cancellationToken
        );
        Assert.That(household.Chores.Keys, Does.Not.Contain("Sleep"));
    }

    [Test, CancelAfter(10_000)]
    public async Task RemoveChorePreservesOtherChores(CancellationToken cancellationToken)
    {
        var store = GetStore();
        await store.Write(_householdAId, [new AddChore("A"), new AddChore("B")], cancellationToken);
        var household = await store.Write(_householdAId, new RemoveChore("A"), cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(household.Chores.Keys, Does.Not.Contain("A"));
            Assert.That(household.Chores.Keys, Contains.Item("B"));
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task RemoveNonExistingChoreThrows(CancellationToken cancellationToken)
    {
        var store = GetStore();
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(_householdAId, new RemoveChore("Ghost"), cancellationToken)
        );
    }
}
