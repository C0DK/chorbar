using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chorbar.Model;

[JsonDerivedType(typeof(AddChore), AddChore.Kind)]
[JsonDerivedType(typeof(RemoveChore), RemoveChore.Kind)]
[JsonDerivedType(typeof(RenameChore), RenameChore.Kind)]
[JsonDerivedType(typeof(DoChore), DoChore.Kind)]
[JsonDerivedType(typeof(SetGoal), SetGoal.Kind)]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Kind")]
public abstract record UserEventPayload
{
    [JsonIgnore]
    public abstract string EventKind { get; }

    public abstract bool IsValid(User user);

    public abstract User Apply(User user, DateTimeOffset timestamp);

    public string Serialize() => JsonSerializer.Serialize(this);

    public static UserEventPayload Deserialize(string payload) =>
        JsonSerializer.Deserialize<UserEventPayload>(payload)!;
}
