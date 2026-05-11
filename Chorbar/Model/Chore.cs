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
}
