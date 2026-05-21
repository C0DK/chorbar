using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record SetGoal(string Chore, int Numerator, DateUnit Unit) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "set_goal";

    public override string EventKind => Kind;

    public override bool IsValid(Household household, DateTimeOffset now) =>
        household.Chores.ContainsKey(Chore);

    public override Household Apply(Household household, DateTimeOffset timestamp)
    {
        var chore = household.Chores[Chore];
        return household with
        {
            Chores = household.Chores.SetItem(
                Chore,
                chore with
                {
                    Goal = new Goal(Numerator, Unit),
                }
            ),
        };
    }
}
