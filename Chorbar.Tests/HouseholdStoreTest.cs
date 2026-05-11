using System.Collections.Immutable;
using Chorbar.Model;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Chorbar.Tests;

public class HouseholdStoreTest
{
    NpgsqlConnection _conn = null!;

    [SetUp]
    public async Task SetUp()
    {
        _conn = await DatabaseFixture.DataSource.OpenConnectionAsync();
        var cancellationToken = CancellationToken.None;
        _timeProvider = new FakeTimeProvider(t(-4)) { AutoAdvanceAmount = _timeStep };
        await using (var cmd = new NpgsqlCommand("TRUNCATE household_event", _conn))
            await cmd.ExecuteNonQueryAsync();
        var store = new HouseholdStore(_conn, new StaticIdentityProvider(_userA), _timeProvider);
        _householdAId = (await store.New("Some Name", cancellationToken));
        _householdAInitialEvents = (
            await store.Write(_householdAId, new AddMember(_userB), cancellationToken)
        ).History;
        _householdBId = (await store.New("Some Other name", CancellationToken.None));
        _householdBInitialEvents = (
            await store.Write(_householdBId, new AddMember(_userB), cancellationToken)
        ).History;
    }

    [TearDown]
    public async Task TearDown() => await _conn.DisposeAsync();

    [Test, CancelAfter(10_000)]
    public async Task PlainGetAfterNewReturnsEmptyHousehold(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);
        var name = "foob";
        var id = await store.New(name, cancellationToken);

        var household = await store.Read(id, cancellationToken);
        Assert.That(
            household,
            Is.EqualTo(
                new Household(
                    id,
                    name,
                    _userA,
                    [_userA],
                    [],
                    [],
                    [new HouseholdEvent(id, 1, t(0), new CreateNewHousehold(name), _userA)]
                )
            )
        );
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
                        new HouseholdEvent(_householdAId, 3, t(0), new AddChore("Sleep"), _userA),
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
            Assert.That(household.Chores["Sleep"], Is.EqualTo(new Chore(t(0), [t(1)])));
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
                new UndoChore("Sleep", t(1)),
            ],
            cancellationToken
        );
        var household = await store.Read(_householdAId, cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(household.Chores.Keys, Is.EquivalentTo(["Sleep"]));
            Assert.That(household.Chores["Sleep"], Is.EqualTo(new Chore(t(0), [t(2)])));
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
            Assert.That(household.Chores["A"], Is.EqualTo(new Chore(t(0), [t(1), t(3)])));
            Assert.That(household.Chores["B"], Is.EqualTo(new Chore(t(2), [t(4)])));
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
                Is.EqualTo(new Chore(t(0), [t(2)], Goal: new Goal(2, DateUnit.Day)))
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
    public async Task TwohouseholdsDoesNotImpactEachother(CancellationToken cancellationToken)
    {
        var storeA = GetStore(_userA);
        var storeB = GetStore(_userB);
        await storeA.Write(_householdAId, [new AddChore("A"), new DoChore("A")], cancellationToken);
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
            Assert.That(householdA.Chores["A"], Is.EqualTo(new Chore(t(0), [t(1)])));
            Assert.That(householdB.Chores["A"], Is.EqualTo(new Chore(t(2), [t(3)])));
            Assert.That(householdB.Chores["B"], Is.EqualTo(new Chore(t(4), [])));
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
    public async Task DoNonExistingActivityThrows(CancellationToken cancellationToken)
    {
        var store = GetStore();

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(_householdAId, new DoChore("Sleep"), cancellationToken)
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task EventRecordsUser(CancellationToken cancellationToken)
    {
        var store = GetStore();
        await store.Write(_householdAId, new AddChore("Sleep"), cancellationToken);

        var household = await store.Read(_householdAId, cancellationToken);
        Assert.That(household.History.Last().CreatedBy, Is.EqualTo(_userA));
    }

    [Test, CancelAfter(10_000)]
    public async Task DifferentUserssRecordedPerEvent(CancellationToken cancellationToken)
    {
        var storeA = GetStore(_userA);
        var storeB = GetStore(_userB);
        await storeA.Write(_householdAId, new AddChore("Sleep"), cancellationToken);
        await storeB.Write(_householdAId, new DoChore("Sleep"), cancellationToken);

        var household = await storeB.Read(_householdAId, cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(
                household.History[household.History.Length - 2].CreatedBy,
                Is.EqualTo(_userA)
            );
            Assert.That(
                household.History[household.History.Length - 1].CreatedBy,
                Is.EqualTo(_userB)
            );
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task ListOnlyOwn(CancellationToken cancellationToken)
    {
        var userA = new Email("a@test.com");
        var userB = new Email("b@test.com");
        var storeA = GetStore(userA);
        var storeB = GetStore(userB);
        var householdAId = await storeA.New("householdA", cancellationToken);
        await storeA.Write(householdAId, new AddMember(userB), cancellationToken);

        var householdA = await storeA.Read(householdAId, cancellationToken);
        var householdB = await storeB.Read(
            await storeB.New("householdB", cancellationToken),
            cancellationToken
        );

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(
                await storeA.List(cancellationToken).Select(h => h.Id).ToArrayAsync(),
                Is.EquivalentTo(new[] { householdA.Id })
            );
            Assert.That(
                await storeB.List(cancellationToken).Select(h => h.Id).ToArrayAsync(),
                Is.EquivalentTo(new[] { householdA.Id, householdB.Id })
            );
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task CannotEditIfNotMember(CancellationToken cancellationToken)
    {
        var id = await GetStore(_userA).New("test", cancellationToken);

        Assert.ThrowsAsync<NotMemberOfHouseholdException>(async () =>
            await GetStore(_userB).Write(id, new DoChore("Sleep"), cancellationToken)
        );
    }

    // --- Rename (household) ---

    [Test, CancelAfter(10_000)]
    public async Task RenameHouseholdChangesName(CancellationToken cancellationToken)
    {
        var store = GetStore();
        await store.Write(_householdAId, new Rename("New Name"), cancellationToken);

        var household = await store.Read(_householdAId, cancellationToken);
        Assert.That(household.Name, Is.EqualTo("New Name"));
    }

    [Test, CancelAfter(10_000)]
    public async Task RenameHouseholdTrimsWhitespace(CancellationToken cancellationToken)
    {
        var store = GetStore();
        await store.Write(_householdAId, new Rename("  Trimmed  "), cancellationToken);

        var household = await store.Read(_householdAId, cancellationToken);
        Assert.That(household.Name, Is.EqualTo("Trimmed"));
    }

    [Test, CancelAfter(10_000)]
    public async Task RenameHouseholdWithBlankNameThrows(CancellationToken cancellationToken)
    {
        var store = GetStore();

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(_householdAId, new Rename("   "), cancellationToken)
        );
    }

    // --- RemoveMember ---

    [Test, CancelAfter(10_000)]
    public async Task RemoveMemberRemovesThem(CancellationToken cancellationToken)
    {
        var store = GetStore();
        var household = await store.Write(
            _householdAId,
            new RemoveMember(_userB),
            cancellationToken
        );

        Assert.That(household.Members, Does.Not.Contain(_userB));
    }

    [Test, CancelAfter(10_000)]
    public async Task RemovedMemberCanNoLongerRead(CancellationToken cancellationToken)
    {
        await GetStore(_userA).Write(_householdAId, new RemoveMember(_userB), cancellationToken);

        Assert.ThrowsAsync<NotMemberOfHouseholdException>(async () =>
            await GetStore(_userB).Read(_householdAId, cancellationToken)
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task RemoveNonMemberThrows(CancellationToken cancellationToken)
    {
        var store = GetStore();
        var nonMember = new Email("stranger@example.com");

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(_householdAId, new RemoveMember(nonMember), cancellationToken)
        );
    }

    // --- RemoveChore ---

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

    // --- AddChore edge cases ---

    [Test, CancelAfter(10_000)]
    public async Task AddDuplicateChoreThrows(CancellationToken cancellationToken)
    {
        var store = GetStore();
        await store.Write(_householdAId, new AddChore("Sleep"), cancellationToken);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(_householdAId, new AddChore("Sleep"), cancellationToken)
        );
    }

    // --- RenameChore edge cases ---

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

    // --- UndoChore edge cases ---

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
    public async Task CannotReadIfNotMember(CancellationToken cancellationToken)
    {
        var id = await GetStore(_userA).New("test", cancellationToken);

        Assert.ThrowsAsync<NotMemberOfHouseholdException>(async () =>
            await GetStore(_userB).Read(id, cancellationToken)
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task CanEditAndReadIfAdded(CancellationToken cancellationToken)
    {
        var name = "test";
        var id = await GetStore(_userA).New(name, cancellationToken);
        await GetStore(_userA).Write(id, new AddMember(_userB), cancellationToken);

        await GetStore(_userB).Write(id, new AddChore("blah"), cancellationToken);

        var household = await GetStore(_userB).Read(id, cancellationToken);
        Assert.That(
            household,
            Is.EqualTo(
                new Household(
                    Id: id,
                    Name: name,
                    Creator: _userA,
                    Members: [_userA, _userB],
                    Chores: ImmutableDictionary<string, Chore>.Empty.Add(
                        "blah",
                        new Chore(t(2), [], null)
                    ),
                    ShoppingListItems: [],
                    History:
                    [
                        new HouseholdEvent(id, 1, t(0), new CreateNewHousehold(name), _userA),
                        new HouseholdEvent(id, 2, t(1), new AddMember(_userB), _userA),
                        new HouseholdEvent(id, 3, t(2), new AddChore("blah"), _userB),
                    ]
                )
            )
        );
    }

    // --- DeleteHousehold ---

    [Test, CancelAfter(10_000)]
    public async Task DeleteHouseholdMarksItAsDeleted(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);
        await store.Delete(_householdAId, cancellationToken);

        Assert.ThrowsAsync<HouseholdNotFoundException>(async () =>
            await store.Read(_householdAId, cancellationToken)
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task DeleteHouseholdIsExcludedFromList(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);
        await store.Delete(_householdAId, cancellationToken);

        var ids = await store.List(cancellationToken).Select(h => h.Id).ToArrayAsync();
        Assert.That(ids, Does.Not.Contain(_householdAId));
    }

    [Test, CancelAfter(10_000)]
    public async Task DeleteHouseholdIsExcludedFromListForAllMembers(
        CancellationToken cancellationToken
    )
    {
        await GetStore(_userA).Delete(_householdAId, cancellationToken);

        var ids = await GetStore(_userB).List(cancellationToken).Select(h => h.Id).ToArrayAsync();
        Assert.That(ids, Does.Not.Contain(_householdAId));
    }

    [Test, CancelAfter(10_000)]
    public async Task DeleteAlreadyDeletedHouseholdThrows(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);
        await store.Delete(_householdAId, cancellationToken);

        Assert.ThrowsAsync<HouseholdNotFoundException>(async () =>
            await store.Delete(_householdAId, cancellationToken)
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task NonMemberCannotDeleteHousehold(CancellationToken cancellationToken)
    {
        var id = await GetStore(_userA).New("to-delete", cancellationToken);

        Assert.ThrowsAsync<NotMemberOfHouseholdException>(async () =>
            await GetStore(_userB).Delete(id, cancellationToken)
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task DeleteOnlyAffectsTargetHousehold(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);
        await store.Delete(_householdAId, cancellationToken);

        var household = await store.Read(_householdBId, cancellationToken);
        Assert.That(household.Id, Is.EqualTo(_householdBId));
    }

    private static TimeSpan _timeStep = TimeSpan.FromMinutes(1);

    private DateTimeOffset t(int i) =>
        new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero).Add(_timeStep * i);

    private FakeTimeProvider _timeProvider = null!;

    private HouseholdStore GetStore() => GetStore(_userA);

    private HouseholdStore GetStore(Email identity) =>
        new HouseholdStore(_conn, new StaticIdentityProvider(identity), _timeProvider);

    private HouseholdId _householdAId;
    private HouseholdId _householdBId;
    private ImmutableArray<HouseholdEvent> _householdAInitialEvents;
    private ImmutableArray<HouseholdEvent> _householdBInitialEvents;
    private static Email _userA => new Email("alice@example.com");
    private static Email _userB => new Email("bob@example.com");
}
