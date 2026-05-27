using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record OrderMealPlan(ImmutableArray<int> ItemIds) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "order_meal_plan";

    public override string EventKind => Kind;

    public override bool IsValid(Household household, DateTimeOffset now) =>
        ItemIds.All(itemId => household.MealPlan.Any(item => item.Id == itemId));

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            MealPlan = household
                .MealPlan.Select(item =>
                {
                    var index = ItemIds.IndexOf(item.Id);
                    return index >= 0 ? item with { Order = index } : item;
                })
                .ToImmutableArray(),
        };
}
