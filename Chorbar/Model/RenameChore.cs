using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record RenameChore(string OldLabel, string NewLabel) : UserEventPayload
{
    [JsonIgnore]
    public const string Kind = "rename_chore";

    public override string EventKind => Kind;

    public override bool IsValid(User user) =>
        user.Chores.ContainsKey(OldLabel) && !user.Chores.ContainsKey(NewLabel);

    public override User Apply(User user, DateTimeOffset timestamp) =>
        user with
        {
            Chores = user.Chores.Remove(OldLabel).Add(NewLabel, user.Chores[OldLabel]),
        };
}
