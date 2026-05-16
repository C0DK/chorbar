using System.Text.RegularExpressions;
using Chorbar.Model;
using Chorbar.Templates;
using Strongbars.Abstractions;

namespace Chorbar.Controllers;

internal static class ViewHelpers
{
    public static EditHousehold EditPage(
        Household household,
        Email currentUser,
        string? baseUrl = null
    ) =>
        new EditHousehold(
            name: household.Name,
            shoppingListEnabled: household.ShoppingListEnabled,
            members: household.Members.Select(m => new HouseholdMemberEntity(
                email: m.ToString(),
                removable: m != household.Creator && m != currentUser
            )),
            hasIcalToken: household.IcalToken is not null,
            icalUrl: household.IcalToken is not null && baseUrl is not null
                ? $"{baseUrl}/ical/{household.Id.Value}/{household.IcalToken}"
                : null
        );

    public static ChoreCard ChoreCard(KeyValuePair<string, Chore> chore) =>
        ChoreCard(chore.Key, chore.Value, false);

    public static ChoreCard ChoreCard(
        string label,
        Chore chore,
        bool oob = false,
        string? oobSwapId = null
    ) =>
        new ChoreCard(
            htmlId: ChoreHtmlId(label),
            order: Convert
                .ToInt32(
                    (
                        (chore.Deadline() ?? (DateTimeOffset.UtcNow.AddDays(3)))
                        - DateTimeOffset.UtcNow
                    ).TotalMinutes
                )
                .ToString(),
            label: label,
            badges: ChoreBadges(chore),
            oobSwap: oob,
            oobSwapId: oobSwapId
        );

    public static string ChoreHtmlId(string label) => $"chore_{GenerateSlug(label)}";

    public static string GenerateSlug(string phrase)
    {
        string str = phrase.ToLowerInvariant();
        // invalid chars
        str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
        // convert multiple spaces into one space
        str = Regex.Replace(str, @"\s+", " ").Trim();
        // cut and trim
        str = str.Substring(0, str.Length <= 45 ? str.Length : 45).Trim();
        str = Regex.Replace(str, @"\s", "-"); // hyphens
        return str;
    }

    private static IEnumerable<TemplateArgument> ChoreBadges(Chore chore)
    {
        if (chore.History.Length > 0)
            yield return new ChoreBadge(
                content: $"x{chore.History.Length}",
                additionalClasses: Array.Empty<string>()
            );
        if (chore.Goal is not null)
        {
            yield return new ChoreBadge(
                content: $"📅 {TimeUntil(chore.Deadline())}",
                additionalClasses: (chore.Deadline() - DateTimeOffset.UtcNow)
                < TimeSpan.FromHours(30)
                    ? ["danger"]
                    : Array.Empty<string>()
            );
        }
    }

    public static EditChore EditChore(string label, Chore chore) =>
        new EditChore(
            htmlId: ChoreHtmlId(label),
            label: label,
            renameForm: new EditChoreRenameForm(label: label),
            actions: chore.History.Select(timestamp => new ChoreActivity(
                timeAgo: TimeAgo(timestamp),
                timestamp: timestamp.ToString("O"),
                label: label
            )),
            // badges are annoying to update for the form..
            //badges: ChoreBadges(chore),
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
