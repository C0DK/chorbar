using System.Text.Json.Serialization;

namespace Chorbar.Model;

// TODO: test
public record RemoveChore(string Label) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "remove_chore";

    public override string EventKind => Kind;

    public override bool IsValid(Household household) => household.Chores.ContainsKey(Label);

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            Chores = household.Chores.Remove(Label),
        };
}
