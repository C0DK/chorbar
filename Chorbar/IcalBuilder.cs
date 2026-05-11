using System.Text;
using Chorbar.Model;

namespace Chorbar;

public static class IcalBuilder
{
    public static string Build(Household household)
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

            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:{uid}");
            sb.AppendLine($"DTSTAMP:{FormatDateTime(now)}");
            sb.AppendLine($"DTSTART;VALUE=DATE:{FormatDate(deadline.Value)}");
            sb.AppendLine($"DTEND;VALUE=DATE:{FormatDate(deadline.Value.AddDays(1))}");
            sb.AppendLine($"SUMMARY:{EscapeText(label)}");
            sb.AppendLine(
                $"DESCRIPTION:Due every {chore.Goal!.Numerator} {chore.Goal.Unit.ToString().ToLower()}(s) - household: {EscapeText(household.Name)}"
            );
            sb.AppendLine("END:VEVENT");
        }

        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    private static string FormatDateTime(DateTimeOffset dt) =>
        dt.UtcDateTime.ToString("yyyyMMddTHHmmssZ");

    private static string FormatDate(DateTimeOffset dt) => dt.UtcDateTime.ToString("yyyyMMdd");

    private static string EscapeText(string text) =>
        text.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n");
}
