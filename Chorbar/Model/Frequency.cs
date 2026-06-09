namespace Chorbar.Model;

public record Frequency(decimal Numerator, DateUnit Unit)
{
    public override string ToString() =>
        Numerator switch
        {
            >= 1 => $"{Numerator:0.#}/{Unit.Letter()}",
            _ => $"{1 / Numerator:0.#}{Unit.Letter()}",
        };
}
