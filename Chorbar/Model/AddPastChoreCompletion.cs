using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record AddPastChoreCompletion(string Label, DateOnly Date) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "add_past_chore_completion";

    public override string EventKind => Kind;

    public override bool IsValid(Household household, DateTimeOffset now) =>
        household.Chores.ContainsKey(Label) && MidnightOnDate <= now;

    public override Household Apply(Household household, DateTimeOffset timestamp)
    {
        var chore = household.Chores[Label];
        return household with
        {
            Chores = household.Chores.SetItem(
                Label,
                chore with
                {
                    History = chore.History.Add(MidnightOnDate),
                }
            ),
        };
    }

    private DateTimeOffset MidnightOnDate =>
        new DateTimeOffset(Date, TimeOnly.MinValue, TimeSpan.Zero);
}
