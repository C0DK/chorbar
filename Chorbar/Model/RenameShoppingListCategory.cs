using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record RenameShoppingListCategory(string Category, string NewCategory)
    : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "rename_shopping_list_category";

    public override string EventKind => Kind;

    public override bool IsValid(Household household) =>
        !string.IsNullOrWhiteSpace(NewCategory)
        && household.ShoppingListCategories.Contains(Category);

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            ShoppingListCategories = household
                .ShoppingListCategories.Remove(Category)
                .Add(NewCategory.Trim()),
            ShoppingListItems = household
                .ShoppingListItems.Select(item =>
                    item.Category == Category ? item with { Category = NewCategory } : item
                )
                .ToImmutableArray(),
        };
}
