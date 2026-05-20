using System.Collections.Immutable;

namespace Chorbar.Model;

public record Chore(
    DateTimeOffset Created,
    ImmutableArray<DateTimeOffset> History,
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

    // TODO: set "do we achieve match goal"
    public IEnumerable<TimeSpan> Intervals()
    {
        var sorted = History.Append(Created).OrderBy(t => t).ToList();
        if (sorted.Count < 2)
            return [];

        return sorted.Zip(sorted.Skip(1), (a, b) => b - a).OrderBy(t => t);
    }

    public TimeSpan Frequency()
    {
        var intervals = Intervals().ToArray();
        if (intervals.Length == 0)
            return TimeSpan.Zero;

        return intervals[intervals.Length / 2];
    }

    public DateTimeOffset? Deadline() => Goal?.Deadline(History.IsEmpty ? Created : History.Last());

    public TimeSpan WorstFrequency() => Intervals().LastOrDefault(TimeSpan.Zero);

    public Streak? Streak(DateTimeOffset now)
    {
        if (Goal is null)
            return null;
        var streakStart = now;

        foreach (var activity in History.OrderByDescending(a => a))
        {
            if (Goal.IsWithinInterval(activity, streakStart))
                streakStart = activity;
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
