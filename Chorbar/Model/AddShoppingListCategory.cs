using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record AddShoppingListCategory(string Category) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "add_shopping_list_category";

    public override string EventKind => Kind;

    public override bool IsValid(Household household) =>
        !string.IsNullOrWhiteSpace(Category)
        && !household.ShoppingListCategories.Contains(Category);

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            ShoppingListCategories = household.ShoppingListCategories.Add(Category.Trim()),
        };
}
