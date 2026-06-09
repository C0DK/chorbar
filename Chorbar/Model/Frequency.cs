namespace Chorbar.Model;

public record Frequency(decimal Numerator, DateUnit Unit)
{
    public override string ToString() => $"{Numerator:F1}/{Unit.Letter()}";
}
