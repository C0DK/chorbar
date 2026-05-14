using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record SetShoppingListItemCategory(int ItemId, string? Category) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "set_shopping_list_item_category";

    public override string EventKind => Kind;

    public override bool IsValid(Household household) =>
        household.ShoppingListItems.Any(item => item.Id == ItemId)
        && (Category is null || household.ShoppingListCategories.Contains(Category));

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            ShoppingListItems = household
                .ShoppingListItems.Select(item =>
                    item.Id == ItemId ? item with { Category = Category } : item
                )
                .ToImmutableArray(),
        };
}
