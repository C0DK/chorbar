using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record AddChore(string Label) : UserEventPayload
{
    [JsonIgnore]
    public const string Kind = "add_chore";

    public override string EventKind => Kind;

    public override bool IsValid(User user) => !user.Chores.ContainsKey(Label);

    public override User Apply(User user, DateTimeOffset timestamp) =>
        user with
        {
            Chores = user.Chores.Add(Label, new Chore(timestamp, history: [])),
        };
}
