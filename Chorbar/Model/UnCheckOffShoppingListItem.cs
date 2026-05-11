using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record UnCheckOffShoppingListItem(int ItemId) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "un_check_off_shopping_list";

    public override string EventKind => Kind;

    public override bool IsValid(Household household, DateTimeOffset now) =>
        household.ShoppingListItems.Any(item => item.Id == ItemId);

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            ShoppingListItems = household
                .ShoppingListItems.Select(item =>
                    item.Id == ItemId ? item with { Checked = null } : item
                )
                .ToImmutableArray(),
        };
}
