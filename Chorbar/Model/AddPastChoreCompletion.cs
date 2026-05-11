using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record AddPastChoreCompletion(string Label, DateOnly Date) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "add_past_chore_completion";

    public override string EventKind => Kind;

    public override bool IsValid(Household household, DateTimeOffset now) =>
        household.Chores.ContainsKey(Label)
        && new DateTimeOffset(Date.ToDateTime(TimeOnly.Midnight), TimeSpan.Zero) <= now;

    public override Household Apply(Household household, DateTimeOffset timestamp)
    {
        var chore = household.Chores[Label];
        var completionTime = new DateTimeOffset(Date.ToDateTime(TimeOnly.Midnight), TimeSpan.Zero);
        return household with
        {
            Chores = household.Chores.SetItem(
                Label,
                chore with
                {
                    History = chore.History.Add(completionTime),
                }
            ),
        };
    }
}
