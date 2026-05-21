using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record RenameChore(string OldLabel, string NewLabel) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "rename_chore";

    public override string EventKind => Kind;

    public override bool IsValid(Household household, DateTimeOffset now) =>
        household.Chores.ContainsKey(OldLabel) && !household.Chores.ContainsKey(NewLabel);

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            Chores = household.Chores.Remove(OldLabel).Add(NewLabel, household.Chores[OldLabel]),
        };
}
