using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record RenameMealPlanEntry(int Id, string NewLabel) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "rename_meal_plan_entry";

    public override string EventKind => Kind;

    public override bool IsValid(Household household, DateTimeOffset now) =>
        household.MealPlanEnabled && household.MealPlan.Any(m => m.Id == Id);

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            MealPlan = household
                .MealPlan.Select(m => m.Id == Id ? m with { Label = NewLabel } : m)
                .ToImmutableArray(),
        };
}
