using System.Collections.Immutable;

namespace Chorbar.Model;

public record Household(
    HouseholdId Id,
    string Name,
    Email Creator,
    ImmutableHashSet<Email> Members,
    ImmutableDictionary<string, Chore> Chores,
    ImmutableArray<ShoppingListItem> ShoppingListItems,
    ImmutableArray<HouseholdEvent> History,
    int ShoppingListNextId = 1
)
{
    public ImmutableArray<ShoppingListItem> ShoppingList =>
        ShoppingListItems.Where(item => item.Checked is null).ToImmutableArray();

    public ImmutableArray<ShoppingListItem> RecentlyCheckedItems() =>
        RecentlyCheckedItems(TimeProvider.System);

    public ImmutableArray<ShoppingListItem> RecentlyCheckedItems(TimeProvider timeProvider) =>
        ShoppingListItems.Where(item => item.CheckedOffRecently(timeProvider)).ToImmutableArray();

    public virtual bool Equals(Household? other) =>
        other is not null
        && Id == other.Id
        && Name == other.Name
        && Members.SetEquals(other.Members)
        && Chores.Count == other.Chores.Count
        && Chores.All(kv => other.Chores.TryGetValue(kv.Key, out var v) && kv.Value == v)
        && History.SequenceEqual(other.History);

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() =>
        $"""
            '{Name}' #{Id}
            Members: [{string.Join(", ", Members.Select(m => m.Value))}]
            Chores:
              {string.Join("\n  ", Chores.Select(c => $"- {c}"))}
            History:
              {string.Join("\n  ", History.Select(h => $"- {h}"))}
            """;
}
