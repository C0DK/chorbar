using System.Collections.Immutable;

namespace Chorbar.Model;

public record UserSettings(
    Email Email,
    string? DisplayName,
    bool WantReminderEmails,
    ImmutableArray<UserEvent> History
)
{
    public static UserSettings Create(Email email) =>
        new UserSettings(Email: email, DisplayName: null, WantReminderEmails: true, History: []);
}
