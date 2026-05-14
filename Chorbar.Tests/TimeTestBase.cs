namespace Chorbar.Tests;

public abstract class TimeTestBase
{
    protected static readonly TimeSpan TimeStep = TimeSpan.FromMinutes(1);
    private static readonly DateTimeOffset BaseTime = new(2024, 01, 01, 0, 0, 0, TimeSpan.Zero);

    protected DateTimeOffset T(int i) => BaseTime.Add(TimeStep * i);
}
