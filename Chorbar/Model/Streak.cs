namespace Chorbar.Model;

public record Streak(int Numerator, DateUnit Unit)
{
    public override string ToString() =>
        Unit switch
        {
            DateUnit.Week => $"{Numerator}w",
            DateUnit.Month => $"{Numerator}m",
            DateUnit.Year => $"{Numerator}y",
            _ => $"{Numerator}d",
        };
}
