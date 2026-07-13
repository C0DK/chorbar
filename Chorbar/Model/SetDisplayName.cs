using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record SetDisplayName(string DisplayName) : UserEventPayload
{
    [JsonIgnore]
    public const string Kind = "set_display_name";

    public override string EventKind => Kind;

    public override bool IsValid(UserSettings user) => true;

    public override UserSettings Apply(UserSettings entity, DateTimeOffset timestamp) =>
        entity with
        {
            DisplayName = DisplayName,
        };

    public override UserInfo Apply(UserInfo entity, DateTimeOffset timestamp) =>
        entity with
        {
            DisplayName = DisplayName,
        };
}
