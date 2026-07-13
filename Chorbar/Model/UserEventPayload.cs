using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chorbar.Model;

[JsonDerivedType(typeof(SetDisplayName), SetDisplayName.Kind)]
[JsonDerivedType(typeof(SetWantEmailReminders), SetWantEmailReminders.Kind)]
public abstract record UserEventPayload
{
    [JsonIgnore]
    public abstract string EventKind { get; }

    public abstract bool IsValid(UserSettings user);

    public abstract UserSettings Apply(UserSettings entity, DateTimeOffset timestamp);

    public abstract UserInfo Apply(UserInfo entity, DateTimeOffset timestamp);

    public string Serialize() => JsonSerializer.Serialize(this);

    public static UserEventPayload Deserialize(string payload) =>
        JsonSerializer.Deserialize<UserEventPayload>(payload)!;
}
