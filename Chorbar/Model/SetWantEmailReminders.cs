using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record SetWantEmailReminders(bool Value) : UserEventPayload
{
    [JsonIgnore]
    public const string Kind = "set_want_email_reminders";

    public override string EventKind => Kind;

    public override bool IsValid(UserSettings user) => true;

    public override UserSettings Apply(UserSettings entity, DateTimeOffset timestamp) =>
        entity with
        {
            WantReminderEmails = Value,
        };

    public override UserInfo Apply(UserInfo entity, DateTimeOffset timestamp) => entity;
}
