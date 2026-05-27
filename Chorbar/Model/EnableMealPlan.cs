using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record EnableMealPlan(bool enabled) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "enable_meal_plan";

    public override string EventKind => Kind;

    public override bool IsValid(Household household, DateTimeOffset now) => true;

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with { MealPlanEnabled = enabled };
}
