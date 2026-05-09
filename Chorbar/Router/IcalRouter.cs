using System.Text;
using Chorbar.Model;

namespace Chorbar.Routes;

public static class IcalRouter
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet(
            "/{householdId:int}/{token}",
            async Task<IResult> (
                HouseholdId householdId,
                string token,
                HouseholdStore store,
                CancellationToken cancellationToken
            ) =>
            {
                var household = await store.ReadForIcal(householdId, token, cancellationToken);
                if (household is null)
                    return Results.NotFound();

                var ical = BuildIcal(household);
                return Results.Content(ical, "text/calendar; charset=utf-8");
            }
        )
        .AllowAnonymous();
    }

    private static string BuildIcal(Household household)
    {
        var sb = new StringBuilder();
        var now = DateTimeOffset.UtcNow;

        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Chorbar//Chorbar//EN");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");
        sb.AppendLine($"X-WR-CALNAME:{EscapeText(household.Name)} Chores");

        foreach (var (label, chore) in household.Chores)
        {
            var deadline = chore.Deadline();
            if (deadline is null)
                continue;

            var uid = $"{household.Id.Value}-{Uri.EscapeDataString(label)}@chorbar";
            var dtStamp = FormatDateTime(now);
            var dtStart = FormatDate(deadline.Value);
            var dtEnd = FormatDate(deadline.Value.AddDays(1));

            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:{uid}");
            sb.AppendLine($"DTSTAMP:{dtStamp}");
            sb.AppendLine($"DTSTART;VALUE=DATE:{dtStart}");
            sb.AppendLine($"DTEND;VALUE=DATE:{dtEnd}");
            sb.AppendLine($"SUMMARY:{EscapeText(label)}");
            sb.AppendLine($"DESCRIPTION:Due every {chore.Goal!.Numerator} {chore.Goal.Unit.ToString().ToLower()}(s) - household: {EscapeText(household.Name)}");
            sb.AppendLine("END:VEVENT");
        }

        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    private static string FormatDateTime(DateTimeOffset dt) =>
        dt.UtcDateTime.ToString("yyyyMMddTHHmmssZ");

    private static string FormatDate(DateTimeOffset dt) =>
        dt.UtcDateTime.ToString("yyyyMMdd");

    private static string EscapeText(string text) =>
        text.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n");
}
