using Chorbar.Model;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Chorbar.Tests;

public class ShoppingListTest
{
    [Test, CancelAfter(10_000)]
    public async Task ShoppingListEmptyInitially(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        var household = await store.Read(_householdId, cancellationToken);
        Assert.That(household.ShoppingListItems, Is.Empty);
    }

    [Test, CancelAfter(10_000)]
    public async Task AddItem(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        const string label = "Milk";
        var household = await store.Write(
            _householdId,
            [new AddToShoppingList(label)],
            cancellationToken
        );
        Assert.That(
            household.ShoppingListItems,
            Is.EquivalentTo<ShoppingListItem>([new(1, label, null)])
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task AddSameLabelTwice(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        const string label = "Milk";
        var household = await store.Write(
            _householdId,
            [new AddToShoppingList(label), new AddToShoppingList(label)],
            cancellationToken
        );
        Assert.That(
            household.ShoppingListItems,
            Is.EquivalentTo<ShoppingListItem>([new(1, label, null), new(2, label, null)])
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task CheckOffItemDoesntTakeDuplicates(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        const string label = "Milk";
        var household = await store.Write(
            _householdId,
            [
                new AddToShoppingList(label),
                new AddToShoppingList(label),
                new CheckOffShoppingListItem(1),
            ],
            cancellationToken
        );
        Assert.That(
            household.ShoppingListItems,
            Is.EquivalentTo<ShoppingListItem>([new(1, label, t(2)), new(2, label, null)])
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task CheckOffTwiceDoesntUpdateTime(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        const string label = "Milk";
        var householdPre = await store.Write(
            _householdId,
            [new AddToShoppingList(label), new CheckOffShoppingListItem(1)],
            cancellationToken
        );
        var householdAfter = await store.Write(
            _householdId,
            [new CheckOffShoppingListItem(1)],
            cancellationToken
        );
        Assert.That(
            householdPre.ShoppingListItems,
            Is.EquivalentTo(householdAfter.ShoppingListItems)
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task UnCheckOff(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        const string label = "Milk";
        var household = await store.Write(
            _householdId,
            [
                new AddToShoppingList(label),
                new CheckOffShoppingListItem(1),
                new UnCheckOffShoppingListItem(1),
            ],
            cancellationToken
        );
        Assert.That(
            household.ShoppingListItems,
            Is.EquivalentTo<ShoppingListItem>([new(1, label, null)])
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task Rename(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        const string label = "Milk";
        const string newLabel = "Milkyway";
        var household = await store.Write(
            _householdId,
            [
                new AddToShoppingList(label),
                new CheckOffShoppingListItem(1),
                new RenameShoppingListItem(1, newLabel),
                new UnCheckOffShoppingListItem(1),
            ],
            cancellationToken
        );
        Assert.That(
            household.ShoppingListItems,
            Is.EquivalentTo<ShoppingListItem>([new(1, newLabel, null)])
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task ShoppingListOnlyContainsUnchecked(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        const string label = "Milk";
        var household = await store.Write(
            _householdId,
            [
                new AddToShoppingList(label),
                new AddToShoppingList(label),
                new AddToShoppingList(label),
                new CheckOffShoppingListItem(2),
                new CheckOffShoppingListItem(3),
                new UnCheckOffShoppingListItem(3),
            ],
            cancellationToken
        );
        Assert.That(
            household.ShoppingList,
            Is.EquivalentTo<ShoppingListItem>([new(1, label, null), new(3, label, null)])
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task RecentlyCheckedOff(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        const string label = "Milk";
        await store.Write(
            _householdId,
            [
                new AddToShoppingList(label),
                new AddToShoppingList(label),
                new CheckOffShoppingListItem(1),
            ],
            cancellationToken
        );
        _timeProvider.SetUtcNow(t(10_000));

        var household = await store.Write(
            _householdId,
            [new CheckOffShoppingListItem(2)],
            cancellationToken
        );

        Assert.That(
            household.RecentlyCheckedItems(_timeProvider),
            Is.EquivalentTo<ShoppingListItem>([new(2, label, t(10_000))])
        );
    }

    NpgsqlConnection _conn = null!;

    [SetUp]
    public async Task SetUp()
    {
        _conn = await DatabaseFixture.DataSource.OpenConnectionAsync();
        var cancellationToken = CancellationToken.None;
        _timeProvider = new FakeTimeProvider(t(-1)) { AutoAdvanceAmount = _timeStep };
        await using (var cmd = new NpgsqlCommand("TRUNCATE household_event", _conn))
            await cmd.ExecuteNonQueryAsync();
        var store = GetStore(_userA);
        _householdId = (await store.New("Some Name", cancellationToken));
    }

    [TearDown]
    public async Task TearDown() => await _conn.DisposeAsync();

    private HouseholdId _householdId;
    private static Email _userA => new Email("alice@example.com");
    private static Email _userB => new Email("bob@example.com");

    private static TimeSpan _timeStep = TimeSpan.FromMinutes(1);

    private DateTimeOffset t(int i) =>
        new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero).Add(_timeStep * i);

    private FakeTimeProvider _timeProvider = null!;

    private HouseholdStore GetStore(Email identity) =>
        new HouseholdStore(_conn, new StaticIdentityProvider(identity), _timeProvider);
}
