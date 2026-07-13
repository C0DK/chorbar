using System.Text.RegularExpressions;
using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
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
            todoListEnabled: household.TodoListEnabled,
            members: household.Members.Select(m => new HouseholdMemberEntity(
                email: m.ToString(),
                removable: m != household.Creator && m != currentUser
            )),
            hasIcalToken: household.IcalToken is not null,
            icalUrl: household.IcalToken is not null && baseUrl is not null
                ? $"{baseUrl}/ical/{household.Id.Value}/{household.IcalToken}"
                : null
        );

    public static async ValueTask<EditHousehold> EditPage(
        UserReader userReader,
        Household household,
        Email currentUser,
        string? baseUrl = null,
        CancellationToken cancellationToken = default
    ) =>
        new EditHousehold(
            name: household.Name,
            shoppingListEnabled: household.ShoppingListEnabled,
            todoListEnabled: household.TodoListEnabled,
            members: await household.Members.SelectAsync(async m => new HouseholdMemberEntity(
                email: await userReader.DisplayName(m, cancellationToken),
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
                        (chore.Deadline()?.GetMidnightUtc() ?? DateTimeOffset.UtcNow.AddDays(3))
                        - DateTimeOffset.UtcNow
                    ).TotalMinutes
                )
                .ToString(),
            label: label,
            badges: ChoreBadges(chore),
            oobSwap: oob,
            oobSwapId: oobSwapId ?? ChoreHtmlId(label)
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
        if (chore.Goal is not null)
        {
            var deadline = chore.Deadline()!.Value;
            yield return new ChoreBadge(
                content: $"📅  {DeadlineText(deadline)}",
                additionalClasses:
                [
                    DateTimeOffset.Now.GetCalendarDate().DaysUntil(deadline) <= 1
                        ? "emphasis"
                        : "muted",
                ]
            );
            yield return new ChoreBadge(content: $"🎯 {chore.Goal}", additionalClasses: ["muted"]);
        }

        var streak = chore.Streak(DateTimeOffset.UtcNow);
        if (streak is not null)
            yield return new ChoreBadge(
                content: $"🔥 {streak}",
                additionalClasses: Array.Empty<string>()
            );

        var frequency = chore.Frequency();
        if (frequency is not null)
        {
            yield return new ChoreBadge(
                content: $"x{chore.History.Length}",
                additionalClasses: ["muted"]
            );
            yield return new ChoreBadge(content: $"⏱  {frequency}", additionalClasses: ["muted"]);
            yield return new ChoreBadge(
                content: $"⌛ {TimeAgo(chore.History.Last().Timestamp)}",
                additionalClasses: ["muted"]
            );
        }
    }

    public static string DeadlineText(DateOnly? deadline) =>
        DeadlineText(deadline, TimeProvider.System);

    public static string DeadlineText(DateOnly? deadline, TimeProvider provider)
    {
        if (deadline is null)
            return "never";
        var today = provider.Today();

        var overdue = deadline.Value.DaysUntil(today);
        if (overdue > 1)
        {
            return $"{overdue}d overdue";
        }
        if (overdue > 0)
        {
            return "Overdue";
        }
        if (deadline == today)
            return "Today";
        if (deadline == today.AddDays(1))
            return "Tomorrow";
        if (deadline <= today.AddDays(6))
            return $"{deadline:dddd}";

        var daysUntil = -overdue;

        return daysUntil switch
        {
            < 20 => $"In {daysUntil:N0} days",
            < 300 => deadline.Value.ToString("d MMMM"),
            _ => deadline.Value.ToString("d MMMM yyyy"),
        };
    }

    public static async ValueTask<EditChore> EditChore(
        UserReader userReader,
        string label,
        Chore chore,
        CancellationToken cancellationToken
    ) =>
        new EditChore(
            htmlId: ChoreHtmlId(label),
            label: label,
            actions: await chore
                .History.OrderByDescending(t => t)
                .SelectAsync(async a => new ChoreActivity(
                    timeAgo: TimeAgo(a.Timestamp),
                    timestamp: a.Timestamp.ToString("O"),
                    date: a.Timestamp.ToString("yyyy-MM-dd HH:mm"),
                    label: label,
                    user: await userReader.DisplayName(a.User, cancellationToken)
                )),
            // badges are annoying to update for the form..
            //badges: ChoreBadges(chore),
            goalNumerator: chore.Goal?.Numerator,
            goalUnit: chore?.Goal?.Unit.ToString(),
            today: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
        );

    public static EditChore EditChore(string label, Chore chore) =>
        new EditChore(
            htmlId: ChoreHtmlId(label),
            label: label,
            actions: chore
                .History.OrderByDescending(t => t)
                .Select(a => new ChoreActivity(
                    timeAgo: TimeAgo(a.Timestamp),
                    timestamp: a.Timestamp.ToString("O"),
                    date: a.Timestamp.ToString("yyyy-MM-dd HH:mm"),
                    label: label,
                    user: a.User
                )),
            // badges are annoying to update for the form..
            //badges: ChoreBadges(chore),
            goalNumerator: chore.Goal?.Numerator,
            goalUnit: chore?.Goal?.Unit.ToString(),
            today: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
        );

    public static string TimeAgo(DateTimeOffset? timestamp)
    {
        if (timestamp is null)
            return "never";
        var span = (DateTimeOffset.UtcNow - timestamp.Value);
        // browser time?
        var today = DateTimeOffset.Now.GetCalendarDate();
        var date = timestamp.Value.GetCalendarDate();

        return span switch
        {
            { TotalSeconds: < 60 } => "just now",
            { TotalMinutes: < 2 } => "a few seconds ago",
            { TotalMinutes: < 121 } => $"{span.TotalMinutes:N0} minutes ago",
            { TotalHours: < 4 } => $"{span.TotalHours:N0} hours ago",
            _ when date == today.AddDays(-1) => $"Yesterday",
            { TotalHours: < 40 } => $"{span.TotalHours:N0} hours ago",
            _ when date >= today.AddDays(-6) => $"{date:dddd}",

            { TotalDays: < 14 } => $"{span.TotalDays:N0} days ago",
            { TotalDays: < 100 } when today.Year == date.Year => $"{timestamp.Value:d MMMM}",
            _ => timestamp.Value.ToString("d MMMM yyyy"),
        };
    }
}
