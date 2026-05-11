using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record AddToShoppingList(string Label) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "add_to_shopping_list";

    public override string EventKind => Kind;

    public override bool IsValid(Household household) => !string.IsNullOrWhiteSpace(Label);

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            ShoppingListNextId = household.ShoppingListNextId + 1,
            ShoppingListItems = household.ShoppingListItems.Add(
                new ShoppingListItem(household.ShoppingListNextId, Label.Trim(), null)
            ),
        };
}
