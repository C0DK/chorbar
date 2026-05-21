using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record AddPastChoreCompletion(string Label, DateOnly When) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "add_past_chore_completion";

    public override string EventKind => Kind;

    public override bool IsValid(Household household, DateTimeOffset now) =>
        household.Chores.ContainsKey(Label) && MidnightOn(When) <= now;

    public override Household Apply(Household household, DateTimeOffset timestamp)
    {
        var chore = household.Chores[Label];
        return household with
        {
            Chores = household.Chores.SetItem(
                Label,
                chore with
                {
                    History = chore
                        .History.Add(MidnightOn(When))
                        .OrderBy(a => a)
                        .ToImmutableArray(),
                }
            ),
        };
    }

    private static DateTimeOffset MidnightOn(DateOnly date) =>
        new DateTimeOffset(date, TimeOnly.MinValue, TimeSpan.Zero);
}
