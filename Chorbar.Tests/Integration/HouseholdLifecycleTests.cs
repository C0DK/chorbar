using Chorbar.Model;
using Microsoft.Extensions.Time.Testing;

namespace Chorbar.Tests.Integration;

public class HouseholdLifecycleTests : StoreTestBase
{
    private HouseholdId _householdAId;
    private HouseholdId _householdBId;

    [SetUp]
    public async Task SetUp()
    {
        _timeProvider = new FakeTimeProvider(T(-4)) { AutoAdvanceAmount = TimeStep };
        var store = GetStore(UserA);
        _householdAId = await store.New("Some Name", CancellationToken.None);
        await store.Write(_householdAId, new AddMember(UserB), CancellationToken.None);
        _householdBId = await store.New("Some Other Name", CancellationToken.None);
        await store.Write(_householdBId, new AddMember(UserB), CancellationToken.None);
    }

    [Test, CancelAfter(10_000)]
    public async Task PlainGetAfterNewReturnsEmptyHousehold(CancellationToken cancellationToken)
    {
        var store = GetStore(UserA);
        var name = "foob";
        var id = await store.New(name, cancellationToken);

        var household = await store.Read(id, cancellationToken);
        Assert.That(
            household,
            Is.EqualTo(
                new Household(
                    id,
                    name,
                    UserA,
                    [UserA],
                    [],
                    [],
                    [],
                    [new HouseholdEvent(id, 1, T(0), new CreateNewHousehold(name), UserA)]
                )
            )
        );
    }

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

    [Test, CancelAfter(10_000)]
    public async Task EventRecordsUser(CancellationToken cancellationToken)
    {
        var store = GetStore();
        await store.Write(_householdAId, new AddChore("Sleep"), cancellationToken);
        var household = await store.Read(_householdAId, cancellationToken);
        Assert.That(household.History.Last().CreatedBy, Is.EqualTo(UserA));
    }

    [Test, CancelAfter(10_000)]
    public async Task DifferentUsersRecordedPerEvent(CancellationToken cancellationToken)
    {
        var storeA = GetStore(UserA);
        var storeB = GetStore(UserB);
        await storeA.Write(_householdAId, new AddChore("Sleep"), cancellationToken);
        await storeB.Write(_householdAId, new DoChore("Sleep"), cancellationToken);

        var household = await storeB.Read(_householdAId, cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(
                household.History[household.History.Length - 2].CreatedBy,
                Is.EqualTo(UserA)
            );
            Assert.That(
                household.History[household.History.Length - 1].CreatedBy,
                Is.EqualTo(UserB)
            );
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task DeleteHouseholdMarksItAsDeleted(CancellationToken cancellationToken)
    {
        var store = GetStore(UserA);
        await store.Delete(_householdAId, cancellationToken);
        Assert.ThrowsAsync<HouseholdNotFoundException>(async () =>
            await store.Read(_householdAId, cancellationToken)
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task DeleteHouseholdIsExcludedFromList(CancellationToken cancellationToken)
    {
        var store = GetStore(UserA);
        await store.Delete(_householdAId, cancellationToken);
        var ids = await store.List(cancellationToken).Select(h => h.Id).ToArrayAsync();
        Assert.That(ids, Does.Not.Contain(_householdAId));
    }

    [Test, CancelAfter(10_000)]
    public async Task DeleteHouseholdIsExcludedFromListForAllMembers(
        CancellationToken cancellationToken
    )
    {
        await GetStore(UserA).Delete(_householdAId, cancellationToken);
        var ids = await GetStore(UserB).List(cancellationToken).Select(h => h.Id).ToArrayAsync();
        Assert.That(ids, Does.Not.Contain(_householdAId));
    }

    [Test, CancelAfter(10_000)]
    public async Task DeleteAlreadyDeletedHouseholdThrows(CancellationToken cancellationToken)
    {
        var store = GetStore(UserA);
        await store.Delete(_householdAId, cancellationToken);
        Assert.ThrowsAsync<HouseholdNotFoundException>(async () =>
            await store.Delete(_householdAId, cancellationToken)
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task NonMemberCannotDeleteHousehold(CancellationToken cancellationToken)
    {
        var id = await GetStore(UserA).New("to-delete", cancellationToken);
        Assert.ThrowsAsync<NotMemberOfHouseholdException>(async () =>
            await GetStore(UserB).Delete(id, cancellationToken)
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task DeleteOnlyAffectsTargetHousehold(CancellationToken cancellationToken)
    {
        var store = GetStore(UserA);
        await store.Delete(_householdAId, cancellationToken);
        var household = await store.Read(_householdBId, cancellationToken);
        Assert.That(household.Id, Is.EqualTo(_householdBId));
    }
}
