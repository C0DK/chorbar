namespace Chorbar.Model;

public static class DateTimeExtensionMethods
{
    public static int GetMonthsBetween(this DateTimeOffset from, DateTimeOffset to) =>
        // assuming same timezone FYI
        GetMonthsBetween(from.GetCalendarDate(), to.GetCalendarDate());

    public static int GetMonthsBetween(this DateOnly from, DateOnly to)
    {
        if (from > to)
            return -GetMonthsBetween(to, from);

        var monthDiff = Math.Abs(
            (to.Year * 12 + (to.Month - 1)) - (from.Year * 12 + (from.Month - 1))
        );

        if (from.AddMonths(monthDiff) > to || to.Day < from.Day)
            return monthDiff - 1;
        else
            return monthDiff;
    }

    public static int GetYearsBetween(this DateTimeOffset from, DateTimeOffset to) =>
        GetYearsBetween(from.GetCalendarDate(), to.GetCalendarDate());

    public static int GetYearsBetween(this DateOnly from, DateOnly to)
    {
        if (from > to)
            return -GetYearsBetween(to, from);
        var years = to.Year - from.Year;
        // Subtract one if we haven't reached the anniversary yet this year.
        if (to.Month < from.Month || (to.Month == from.Month && to.Day < from.Day))
            years--;
        return years;
    }

    public static DateTimeOffset GetMidnightUtc(this DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day, 0, 0, 0, TimeSpan.Zero);

    public static DateOnly GetCalendarDate(this DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day);

    public static DateOnly StartOfMonth(this DateOnly value) => new(value.Year, value.Month, 1);

    public static TimeOnly GetTime(this DateTimeOffset value) =>
        new(value.Hour, value.Minute, value.Second, value.Millisecond);

    public static DateTimeOffset RoundToMinutes(this DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, 0, TimeSpan.Zero);

    public static DateTimeOffset GetMidnightUtc(this DateOnly value) =>
        new(value.Year, value.Month, value.Day, 0, 0, 0, TimeSpan.Zero);
}
