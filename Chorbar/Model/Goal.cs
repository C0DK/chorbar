namespace Chorbar.Model;

public record Goal(int Numerator, DateUnit Unit)
{
    public override string ToString() => $"{Numerator:0.#}{Unit.Letter()}";

    public DateOnly Deadline(DateTimeOffset lastDone) =>
        (
            Unit switch
            {
                DateUnit.Day => lastDone.AddDays(Numerator),
                DateUnit.Week => lastDone.AddDays(Numerator * 7),
                DateUnit.Month => lastDone.AddMonths(Numerator),
                DateUnit.Year => lastDone.AddYears(Numerator),
                _ => throw new NotImplementedException($"Cannot handle Unit '{Unit}'"),
                // TODO: ideally it should be in user timezone
            }
        ).GetCalendarDate();

    public int GetAllowedLatencyDays() =>
        int.Max(
            1,
            Unit switch
            {
                DateUnit.Day => Numerator,
                DateUnit.Week => Numerator * 7,
                DateUnit.Month => 31,
                DateUnit.Year => 365,
                _ => throw new NotImplementedException($"Cannot handle Unit '{Unit}'"),
            } / 10
        );

    public bool IsWithinInterval(DateTimeOffset lastTime, DateTimeOffset newTime) =>
        newTime.GetCalendarDate() <= Deadline(lastTime).AddDays(GetAllowedLatencyDays());
}
