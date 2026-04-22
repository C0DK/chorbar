using System.Collections.Immutable;

namespace Chorbar.Model;

public record Chore(
    DateTimeOffset created,
    ImmutableArray<DateTimeOffset> history,
    Goal? Goal = null
)
{
    public virtual bool Equals(Chore? other) =>
        other is not null
        && created == other.created
        && Goal == other.Goal
        && history.SequenceEqual(other.history);

    public override int GetHashCode() => HashCode.Combine(created, history.Length);

    // TODO: set "do we achieve match goal"
    public List<TimeSpan> Intervals()
    {
        var sorted = history.Append(created).OrderBy(t => t).ToList();
        if (sorted.Count < 2)
            return [];

        return sorted.Zip(sorted.Skip(1), (a, b) => b - a).OrderBy(t => t).ToList();
    }

    public TimeSpan Frequency()
    {
        var intervals = Intervals();
        if (intervals.Count == 0)
            return TimeSpan.Zero;

        return intervals[intervals.Count / 2];
    }

    public TimeSpan WorstFrequency()
    {
        var intervals = Intervals();
        if (intervals.Count == 0)
            return TimeSpan.Zero;

        return intervals.Last();
    }
}
