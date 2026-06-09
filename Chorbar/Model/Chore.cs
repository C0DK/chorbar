using System.Collections.Immutable;

namespace Chorbar.Model;

public record Chore(
    DateTimeOffset Created,
    ImmutableArray<(DateTimeOffset Timestamp, string User)> History,
    Goal? Goal = null
)
{
    public virtual bool Equals(Chore? other) =>
        other is not null
        && Created == other.Created
        && Goal == other.Goal
        && History.SequenceEqual(other.History);

    public override int GetHashCode() => HashCode.Combine(Created, History.Length);

    public override string ToString() =>
        $"Chore {{ Created = {Created}, History = [{string.Join(", ", History)}], Goal = {Goal} }}";

    public decimal Frequency(DateTimeOffset startDate, DateUnit unit)
    {
        if (History.IsEmpty)
            return 0m;

        var lastDone = History.Last().Timestamp;
        if (lastDone <= startDate)
            return 0m;

        var count = History.Count(h => h.Timestamp > startDate && h.Timestamp <= lastDone);
        var totalDays = (lastDone - startDate).TotalDays;

        var divisor = unit switch
        {
            DateUnit.Day => totalDays,
            DateUnit.Week => totalDays / 7,
            DateUnit.Month => totalDays / (365.25 / 12),
            DateUnit.Year => totalDays / 365.25,
            _ => throw new NotImplementedException($"Cannot handle Unit '{unit}'"),
        };

        if (divisor == 0)
            return 0m;

        return (decimal)(count / divisor);
    }

    public decimal Frequency() =>
        Frequency(Created, Goal?.Unit ?? DateUnit.Day);

    public DateOnly? Deadline() =>
        Goal?.Deadline(History.IsEmpty ? Created : History.Last().Timestamp);

    public Streak? Streak(DateTimeOffset now)
    {
        if (Goal is null)
            return null;
        var streakStart = now;

        foreach (var activity in History.OrderByDescending(a => a))
        {
            if (Goal.IsWithinInterval(activity.Timestamp, streakStart))
                streakStart = activity.Timestamp;
            else
                break;
        }

        var delta = now - streakStart;

        var numerator = Goal.Unit switch
        {
            DateUnit.Day => (int)(now - streakStart).TotalDays,
            DateUnit.Week => ((int)(now - streakStart).TotalDays) / 7,
            DateUnit.Month => streakStart.GetMonthsBetween(now),
            DateUnit.Year => streakStart.GetYearsBetween(now),
            _ => throw new NotImplementedException(),
        };
        if (numerator < Goal.Numerator * 2)
            return null;
        return new Streak(numerator, Goal.Unit);
    }
}
