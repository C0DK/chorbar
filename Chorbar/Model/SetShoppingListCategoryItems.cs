using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record SetShoppingListCategoryItems(string? Category, ImmutableArray<int> ItemIds)
    : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "set_shopping_list_category_items";

    public override string EventKind => Kind;

    public override bool IsValid(Household household) =>
        ItemIds.All(itemId => household.ShoppingListItems.Any(item => item.Id == itemId))
        && (Category is null || household.ShoppingListCategories.Contains(Category));

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            ShoppingListItems = household
                .ShoppingListItems.Select(item =>
                {
                    var index = ItemIds.IndexOf(item.Id);
                    return index >= 0 ? item with { Category = Category, Order = index } : item;
                })
                .ToImmutableArray(),
        };
}
