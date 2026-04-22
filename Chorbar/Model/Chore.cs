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
    public List<TimeSpan> Intervals()
    {
        var sorted = History.Append(Created).OrderBy(t => t).ToList();
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
