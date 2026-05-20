using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record AddChore(string Label) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "add_chore";

    public override string EventKind => Kind;

    public override bool IsValid(Household household, DateTimeOffset now) =>
        !household.Chores.ContainsKey(Label);

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            Chores = household.Chores.Add(Label, new Chore(timestamp, History: [])),
        };
}
