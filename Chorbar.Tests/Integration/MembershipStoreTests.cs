using System.Collections.Immutable;
using Chorbar.Model;
using Microsoft.Extensions.Time.Testing;

namespace Chorbar.Tests.Integration;

public class MembershipStoreTests : StoreTestBase
{
    private HouseholdId _householdAId;

    [SetUp]
    public async Task SetUp()
    {
        _timeProvider = new FakeTimeProvider(T(-2)) { AutoAdvanceAmount = TimeStep };
        var store = GetStore(UserA);
        _householdAId = await store.New("Some Name", CancellationToken.None);
        await store.Write(_householdAId, new AddMember(UserB), CancellationToken.None);
    }

    [Test, CancelAfter(10_000)]
    public async Task RemoveMemberRemovesThem(CancellationToken cancellationToken)
    {
        var store = GetStore();
        var household = await store.Write(
            _householdAId,
            new RemoveMember(UserB),
            cancellationToken
        );
        Assert.That(household.Members, Does.Not.Contain(UserB));
    }

    [Test, CancelAfter(10_000)]
    public async Task RemovedMemberCanNoLongerRead(CancellationToken cancellationToken)
    {
        await GetStore(UserA).Write(_householdAId, new RemoveMember(UserB), cancellationToken);
        Assert.ThrowsAsync<NotMemberOfHouseholdException>(async () =>
            await GetStore(UserB).Read(_householdAId, cancellationToken)
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

    [Test, CancelAfter(10_000)]
    public async Task CannotEditIfNotMember(CancellationToken cancellationToken)
    {
        var id = await GetStore(UserA).New("test", cancellationToken);
        Assert.ThrowsAsync<NotMemberOfHouseholdException>(async () =>
            await GetStore(UserB).Write(id, new DoChore("Sleep"), cancellationToken)
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task CannotReadIfNotMember(CancellationToken cancellationToken)
    {
        var id = await GetStore(UserA).New("test", cancellationToken);
        Assert.ThrowsAsync<NotMemberOfHouseholdException>(async () =>
            await GetStore(UserB).Read(id, cancellationToken)
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task CanEditAndReadIfAdded(CancellationToken cancellationToken)
    {
        var name = "test";
        var id = await GetStore(UserA).New(name, cancellationToken);
        await GetStore(UserA).Write(id, new AddMember(UserB), cancellationToken);
        await GetStore(UserB).Write(id, new AddChore("blah"), cancellationToken);

        var household = await GetStore(UserB).Read(id, cancellationToken);
        Assert.That(
            household,
            Is.EqualTo(
                new Household(
                    Id: id,
                    Name: name,
                    Creator: UserA,
                    Members: [UserA, UserB],
                    Chores: ImmutableDictionary<string, Chore>.Empty.Add(
                        "blah",
                        new Chore(T(2), [], null)
                    ),
                    ShoppingListItems: [],
                    ShoppingListCategories: [],
                    History:
                    [
                        new HouseholdEvent(id, 1, T(0), new CreateNewHousehold(name), UserA),
                        new HouseholdEvent(id, 2, T(1), new AddMember(UserB), UserA),
                        new HouseholdEvent(id, 3, T(2), new AddChore("blah"), UserB),
                    ]
                )
            )
        );
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
}
