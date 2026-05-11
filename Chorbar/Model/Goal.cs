namespace Chorbar.Model;

public record Goal(int Numerator, DateUnit Unit)
{
    public DateTimeOffset Deadline(DateTimeOffset lastDone) =>
        Unit switch
        {
            DateUnit.Day => lastDone.AddDays(Numerator),
            DateUnit.Week => lastDone.AddDays(Numerator * 7),
            DateUnit.Month => lastDone.AddMonths(Numerator),
            DateUnit.Year => lastDone.AddYears(Numerator),
            _ => throw new NotImplementedException($"Cannot handle Unit '{Unit}'"),
        };
}
