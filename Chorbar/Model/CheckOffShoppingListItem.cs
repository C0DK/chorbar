using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record CheckOffShoppingListItem(int ItemId) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "check_off_shopping_list";

    public override string EventKind => Kind;

    public override bool IsValid(Household household) =>
        household.ShoppingListItems.Any(item => item.Id == ItemId);

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            ShoppingListItems = household
                .ShoppingListItems.Select(item =>
                    item.Id == ItemId && item.Checked is null
                        ? item with
                        {
                            Checked = timestamp,
                        }
                        : item
                )
                .ToImmutableArray(),
        };
}
