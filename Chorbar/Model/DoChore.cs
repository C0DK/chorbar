using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record DoChore(string Label) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "do_chore";

    public override string EventKind => Kind;

    public override bool IsValid(Household household) => household.Chores.ContainsKey(Label);

    public override Household Apply(Household household, DateTimeOffset timestamp)
    {
        var chore = household.Chores[Label];
        return household with
        {
            Chores = household.Chores.SetItem(
                Label,
                chore with
                {
                    History = chore.History.Add(timestamp),
                }
            ),
        };
    }
}
