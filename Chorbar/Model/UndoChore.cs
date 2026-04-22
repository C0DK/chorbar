using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record UndoChore(string Label, DateTimeOffset timestamp) : UserEventPayload
{
    [JsonIgnore]
    public const string Kind = "undo_chore";

    public override string EventKind => Kind;

    public override bool IsValid(User user) =>
        user.Chores.ContainsKey(Label) && user.Chores[Label].History.Contains(timestamp);

    public override User Apply(User user, DateTimeOffset eventTime)
    {
        var chore = user.Chores[Label];
        return user with
        {
            Chores = user.Chores.SetItem(
                Label,
                chore with
                {
                    History = chore.History.Remove(timestamp),
                }
            ),
        };
    }
}
