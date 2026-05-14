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
            Is.EquivalentTo<ShoppingListItem>([new(1, label, null, Order: 0)])
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
            Is.EquivalentTo<ShoppingListItem>(
                [new(1, label, null, Order: 0), new(2, label, null, Order: 1)]
            )
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
            Is.EquivalentTo<ShoppingListItem>(
                [new(1, label, t(2), Order: 0), new(2, label, null, Order: 1)]
            )
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
            Is.EquivalentTo<ShoppingListItem>([new(1, label, null, Order: 0)])
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
            Is.EquivalentTo<ShoppingListItem>([new(1, newLabel, Order: 0)])
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
            household.ShoppingList.Single().Items,
            Is.EquivalentTo<ShoppingListItem>([new(1, label, Order: 0), new(3, label, Order: 2)])
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
            Is.EquivalentTo<ShoppingListItem>([new(2, label, t(10_000), Order: 1)])
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task ShoppingListAddCategoryShowsEmpty(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        const string label = "Milk";
        const string category = "Dairy";
        var household = await store.Write(
            _householdId,
            [new AddToShoppingList(label), new AddShoppingListCategory(category)],
            cancellationToken
        );
        Assert.That(
            household.ShoppingList.Select(l => (l.Category, l.Items.Length)),
            Is.EquivalentTo<(string?, int)>([(null, 1), new(category, 0)])
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task ShoppingListSetCategory(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        const string label = "Milk";
        const string category = "Dairy";
        var household = await store.Write(
            _householdId,
            [
                new AddToShoppingList(label),
                new AddShoppingListCategory(category),
                new SetShoppingListItemCategory(1, category),
            ],
            cancellationToken
        );
        Assert.That(
            household.ShoppingList.Select(l => (l.Category, l.Items.Length)),
            Is.EquivalentTo<(string?, int)>([(null, 0), new(category, 1)])
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task SetShoppingListItemCategoryToNullUncategorizes(
        CancellationToken cancellationToken
    )
    {
        var store = GetStore(_userA);

        const string label = "Milk";
        const string category = "Dairy";
        var household = await store.Write(
            _householdId,
            [
                new AddToShoppingList(label),
                new AddShoppingListCategory(category),
                new SetShoppingListItemCategory(1, category),
                new SetShoppingListItemCategory(1, null),
            ],
            cancellationToken
        );
        Assert.That(
            household.ShoppingList.Select(l => (l.Category, l.Items.Length)),
            Is.EquivalentTo<(string?, int)>([(null, 1), (category, 0)])
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task SetShoppingListItemCategoryRejectsUnknownCategory(
        CancellationToken cancellationToken
    )
    {
        var store = GetStore(_userA);

        await store.Write(_householdId, [new AddToShoppingList("Milk")], cancellationToken);
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(
                _householdId,
                [new SetShoppingListItemCategory(1, "Nope")],
                cancellationToken
            )
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task AddDuplicateCategoryRejected(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        await store.Write(_householdId, [new AddShoppingListCategory("Dairy")], cancellationToken);
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(
                _householdId,
                [new AddShoppingListCategory("Dairy")],
                cancellationToken
            )
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task AddBlankCategoryRejected(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(_householdId, [new AddShoppingListCategory("   ")], cancellationToken)
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task SortCategoriesReordersList(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        var household = await store.Write(
            _householdId,
            [
                new AddShoppingListCategory("Dairy"),
                new AddShoppingListCategory("Produce"),
                new AddShoppingListCategory("Bakery"),
                new SortCategories(["Bakery", "Dairy", "Produce"]),
            ],
            cancellationToken
        );
        Assert.That(
            household.ShoppingListCategories,
            Is.EqualTo(new[] { "Bakery", "Dairy", "Produce" })
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task SortCategoriesRejectsUnknownCategory(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        await store.Write(_householdId, [new AddShoppingListCategory("Dairy")], cancellationToken);
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(
                _householdId,
                [new SortCategories(["Dairy", "Ghost"])],
                cancellationToken
            )
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task DeleteCategoryRemovesAndUncategorizesItems(
        CancellationToken cancellationToken
    )
    {
        var store = GetStore(_userA);

        const string category = "Dairy";
        var household = await store.Write(
            _householdId,
            [
                new AddShoppingListCategory(category),
                new AddToShoppingList("Milk"),
                new SetShoppingListItemCategory(1, category),
                new DeleteShoppingListCategory(category),
            ],
            cancellationToken
        );
        Assert.That(household.ShoppingListCategories, Is.Empty);
        Assert.That(household.ShoppingListItems.Single().Category, Is.Null);
    }

    [Test, CancelAfter(10_000)]
    public async Task DeleteCategoryRejectsUnknown(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(
                _householdId,
                [new DeleteShoppingListCategory("Nope")],
                cancellationToken
            )
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task RenameCategoryUpdatesItems(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        const string category = "Dairy";
        const string renamed = "Mejeri";
        var household = await store.Write(
            _householdId,
            [
                new AddShoppingListCategory(category),
                new AddToShoppingList("Milk"),
                new SetShoppingListItemCategory(1, category),
                new RenameShoppingListCategory(category, renamed),
            ],
            cancellationToken
        );
        Assert.That(household.ShoppingListCategories, Is.EqualTo(new[] { renamed }));
        Assert.That(household.ShoppingListItems.Single().Category, Is.EqualTo(renamed));
    }

    [Test, CancelAfter(10_000)]
    public async Task RenameCategoryRejectsBlankNewName(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        await store.Write(_householdId, [new AddShoppingListCategory("Dairy")], cancellationToken);
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(
                _householdId,
                [new RenameShoppingListCategory("Dairy", "   ")],
                cancellationToken
            )
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task RenameCategoryRejectsUnknown(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(
                _householdId,
                [new RenameShoppingListCategory("Dairy", "Mejeri")],
                cancellationToken
            )
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task SetCategoryItemsMovesItemsAndAssignsOrder(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        const string category = "Dairy";
        var household = await store.Write(
            _householdId,
            [
                new AddShoppingListCategory(category),
                new AddToShoppingList("Milk"),
                new AddToShoppingList("Cheese"),
                new AddToShoppingList("Yoghurt"),
                new SetShoppingListCategoryItems(category, [3, 1]),
            ],
            cancellationToken
        );

        var byId = household.ShoppingListItems.ToDictionary(i => i.Id);
        Assert.That(byId[1].Category, Is.EqualTo(category));
        Assert.That(byId[1].Order, Is.EqualTo(1));
        Assert.That(byId[3].Category, Is.EqualTo(category));
        Assert.That(byId[3].Order, Is.EqualTo(0));
        Assert.That(byId[2].Category, Is.Null);
    }

    [Test, CancelAfter(10_000)]
    public async Task SetCategoryItemsToNullUncategorizes(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        const string category = "Dairy";
        var household = await store.Write(
            _householdId,
            [
                new AddShoppingListCategory(category),
                new AddToShoppingList("Milk"),
                new SetShoppingListCategoryItems(category, [1]),
                new SetShoppingListCategoryItems(null, [1]),
            ],
            cancellationToken
        );
        Assert.That(household.ShoppingListItems.Single().Category, Is.Null);
    }

    [Test, CancelAfter(10_000)]
    public async Task SetCategoryItemsRejectsUnknownItem(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        await store.Write(_householdId, [new AddShoppingListCategory("Dairy")], cancellationToken);
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(
                _householdId,
                [new SetShoppingListCategoryItems("Dairy", [99])],
                cancellationToken
            )
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task SetCategoryItemsRejectsUnknownCategory(CancellationToken cancellationToken)
    {
        var store = GetStore(_userA);

        await store.Write(_householdId, [new AddToShoppingList("Milk")], cancellationToken);
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.Write(
                _householdId,
                [new SetShoppingListCategoryItems("Ghost", [1])],
                cancellationToken
            )
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
