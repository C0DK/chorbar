using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record RenameShoppingListItem(int ItemId, string newLabel) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "rename_shopping_list";

    public override string EventKind => Kind;

    public override bool IsValid(Household household) =>
        !string.IsNullOrWhiteSpace(newLabel)
        && household.ShoppingListItems.Any(item => item.Id == ItemId);

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            ShoppingListItems = household
                .ShoppingListItems.Select(item =>
                    item.Id == ItemId ? item with { Label = newLabel.Trim() } : item
                )
                .ToImmutableArray(),
        };
}
