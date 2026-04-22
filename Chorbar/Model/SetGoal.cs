using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record SetGoal(string Chore, int Numerator, DateUnit Unit) : UserEventPayload
{
    [JsonIgnore]
    public const string Kind = "set_goal";

    public override string EventKind => Kind;

    public override bool IsValid(User user) => user.Chores.ContainsKey(Chore);

    public override User Apply(User user, DateTimeOffset timestamp)
    {
        var chore = user.Chores[Chore];
        return user with
        {
            Chores = user.Chores.SetItem(Chore, chore with { Goal = new Goal(Numerator, Unit) }),
        };
    }
}
