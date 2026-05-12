using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record AddToShoppingList(string Label, string? category = null) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "add_to_shopping_list";

    public override string EventKind => Kind;

    public override bool IsValid(Household household) =>
        !string.IsNullOrWhiteSpace(Label)
        && (category is null || household.ShoppingListCategories.Contains(category));

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            ShoppingListNextId = household.ShoppingListNextId + 1,
            ShoppingListItems = household.ShoppingListItems.Add(
                new ShoppingListItem(
                    household.ShoppingListNextId,
                    Label.Trim(),
                    null,
                    category,
                    Order: household.ShoppingListItems.Length > 0
                        ? household.ShoppingListItems.Max(i => i.Order) + 1
                        : 0
                )
            ),
        };
}
