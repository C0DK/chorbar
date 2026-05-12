using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record DeleteShoppingListCategory(string Category) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "delete_shopping_list_category";

    public override string EventKind => Kind;

    public override bool IsValid(Household household) =>
        household.ShoppingListCategories.Contains(Category);

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            ShoppingListCategories = household.ShoppingListCategories.Remove(Category),
            ShoppingListItems = household
                .ShoppingListItems.Select(item =>
                    item.Category == Category ? item with { Category = null } : item
                )
                .ToImmutableArray(),
        };
}
