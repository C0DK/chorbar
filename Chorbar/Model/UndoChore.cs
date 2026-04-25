using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record UndoChore(string Label, DateTimeOffset timestamp) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "undo_chore";

    public override string EventKind => Kind;

    public override bool IsValid(Household household) =>
        household.Chores.ContainsKey(Label) && household.Chores[Label].History.Contains(timestamp);

    public override Household Apply(Household household, DateTimeOffset eventTime)
    {
        var chore = household.Chores[Label];
        return household with
        {
            Chores = household.Chores.SetItem(
                Label,
                chore with
                {
                    History = chore.History.Remove(timestamp),
                }
            ),
        };
    }
}
