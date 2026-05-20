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
        var days = StreakDays(now);
        if (days == 0)
            return null;

        return Goal?.Unit switch
        {
            DateUnit.Week => new Streak(days / 7, DateUnit.Week),
            DateUnit.Month => new Streak(days / 30, DateUnit.Month),
            DateUnit.Year => new Streak(days / 365, DateUnit.Year),
            _ => new Streak(days, DateUnit.Day),
        };
    }

    private int StreakDays(DateTimeOffset now)
    {
        if (History.IsEmpty)
            return 0;

        var sorted = History.OrderBy(t => t).ToList();
        var frequency = Frequency();

        if (frequency == TimeSpan.Zero)
            return (int)(now - sorted[0]).TotalDays;

        var allowedLatencyDays = Math.Max(1.0, frequency.TotalDays / 10.0);
        var maxGap = frequency + TimeSpan.FromDays(allowedLatencyDays);

        if (now - sorted.Last() > maxGap)
            return 0;

        var streakStart = sorted.Last();
        for (var i = sorted.Count - 1; i > 0; i--)
        {
            var gap = sorted[i] - sorted[i - 1];
            if (gap <= maxGap)
                streakStart = sorted[i - 1];
            else
                break;
        }

        return (int)(now - streakStart).TotalDays;
    }
}
