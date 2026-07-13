using Chorbar.Model;

namespace Chorbar.Tests;

public class TestFixture
{
    private static readonly TimeSpan _timeStep = TimeSpan.FromMinutes(1);

    public static DateTimeOffset t(int i) =>
        new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero).Add(_timeStep * i);

    public static DateTimeOffset d(int days) =>
        new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero).AddDays(days);

    public static (DateTimeOffset Timestamp, Email User) h(int days) =>
        (d(days), new Email("test@test.dk"));

    public static (DateTimeOffset Timestamp, Email User) ht(int i) =>
        (t(i), new Email("test@test.dk"));
}
