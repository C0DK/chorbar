using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record AddMealPlanEntry(string Label) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "add_meal_plan_entry";

    public override string EventKind => Kind;

    public override bool IsValid(Household household, DateTimeOffset now) =>
        household.MealPlanEnabled;

    public override Household Apply(Household household, DateTimeOffset timestamp)
    {
        var id = household.MealPlan.Length > 0 ? (household.MealPlan.Max(m => m.Id) + 1) : 1;
        return household with
        {
            MealPlan = household.MealPlan.Add(new MealPlanItem(id, Label, null, id)),
        };
    }
}
