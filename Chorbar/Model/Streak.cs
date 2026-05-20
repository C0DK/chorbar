namespace Chorbar.Model;

public record Streak(int Numerator, DateUnit Unit)
{
    public override string ToString() => $"{Numerator}{Unit.Letter()}";
}
