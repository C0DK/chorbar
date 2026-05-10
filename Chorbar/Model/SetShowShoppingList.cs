using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record EnableShoppingList(bool enabled) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "enable_shopping_list";

    public override string EventKind => Kind;

    public override bool IsValid(Household household) => true;

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            ShoppingListEnabled = enabled,
        };
}
