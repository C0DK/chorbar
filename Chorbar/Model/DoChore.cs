using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record DoChore(string Label) : UserEventPayload
{
    [JsonIgnore]
    public const string Kind = "do_chore";

    public override string EventKind => Kind;

    public override bool IsValid(User user) => user.Chores.ContainsKey(Label);

    public override User Apply(User user, DateTimeOffset timestamp)
    {
        var chore = user.Chores[Label];
        return user with
        {
            Chores = user.Chores.SetItem(
                Label,
                chore with
                {
                    History = chore.History.Add(timestamp),
                }
            ),
        };
    }
}
