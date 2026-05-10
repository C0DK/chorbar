using Chorbar.Model;
using Chorbar.Templates;

namespace Chorbar.Controllers;

internal static class ViewHelpers
{
    public static EditHousehold EditPage(Household household) =>
        new EditHousehold(
            name: household.Name,
            members: household.Members.Select(m => new HouseholdMemberEntity(email: m.ToString()))
        );

    public static ChoreCard ChoreCard(KeyValuePair<string, Chore> chore) =>
        ChoreCard(chore.Key, chore.Value);

    public static ChoreCard ChoreCard(string label, Chore chore) =>
        new ChoreCard(
            label: label,
            count: chore.History.Count(),
            hasDone: chore.History.Count() > 0,
            timeAgo: TimeAgo(chore.History.LastOrDefault()),
            hasGoal: chore.Goal is not null,
            deadline: TimeUntil(chore.Deadline()),
            goalNumerator: chore.Goal?.Numerator,
            goalUnit: chore?.Goal?.Unit.ToString()
        );

    public static ChoreInfo ChoreInfo(string label, Chore chore) =>
        new ChoreInfo(
            label: label,
            actions: chore.History.Select(timestamp => new ChoreActivity(
                timeAgo: TimeAgo(timestamp),
                timestamp: timestamp.ToString("O"),
                label: label
            )),
            count: chore.History.Count(),
            hasDone: chore.History.Count() > 0,
            timeAgo: TimeAgo(chore.History.LastOrDefault()),
            hasGoal: chore.Goal is not null,
            deadline: TimeUntil(chore.Deadline()),
            goalNumerator: chore.Goal?.Numerator,
            goalUnit: chore?.Goal?.Unit.ToString()
        );

    public static string TimeAgo(DateTimeOffset? timestamp)
    {
        if (timestamp is null)
            return "never";
        var span = (DateTimeOffset.UtcNow - timestamp.Value);

        return span switch
        {
            { TotalSeconds: < 60 } => "just now",
            { TotalMinutes: < 2 } => "a few seconds ago",
            { TotalMinutes: < 121 } => $"{span.TotalMinutes:N0} minutes ago",
            { TotalHours: < 49 } => $"{span.TotalHours:N0} hours ago",
            { TotalDays: < 14 } => $"{span.TotalDays:N0} days ago",
            { TotalDays: < 200 } => $"{timestamp.Value:d MMMM}",
            _ => timestamp.Value.ToString("d MMMM yyyy"),
        };
    }

    public static string TimeUntil(DateTimeOffset? timestamp)
    {
        if (timestamp is null)
            return "never";
        var span = (timestamp.Value - DateTimeOffset.UtcNow);

        return span switch
        {
            { TotalSeconds: < 0 } => "now",
            { TotalSeconds: < 60 } => "now",
            { TotalMinutes: < 2 } => "in a few seconds",
            { TotalMinutes: < 121 } => $"in {span.TotalMinutes:N0} minutes",
            { TotalHours: < 49 } => $"in {span.TotalHours:N0} hours",
            { TotalDays: < 20 } => $"in {span.TotalDays:N0} days",
            { TotalDays: < 200 } => $"on {timestamp.Value:d MMMM}",
            _ => $"on {timestamp:d MMMM yyyy}",
        };
    }
}
