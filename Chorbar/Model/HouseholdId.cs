using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chorbar.Model;

// TODO: remove constructor so we always lower it?
[JsonConverter(typeof(HouseholdId.DefaultJsonConverter))]
public readonly record struct HouseholdId(int Value)
{
    public override string ToString() => Value.ToString();

    public static HouseholdId Parse(string value)
    {
        if (TryParse(value, out var email))
            return email;

        throw new InvalidOperationException($"Email '{value}' is not valid!");
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out HouseholdId output)
    {
        if (!int.TryParse(value, out var intValue))
        {
            output = default;
            return false;
        }
        return TryParse(intValue, out output);
    }

    public static bool TryParse(int value, [NotNullWhen(true)] out HouseholdId output)
    {
        if (!IsValid(value!))
        {
            output = default;
            return false;
        }
        output = new(value);
        return true;
    }

    public static implicit operator int(HouseholdId id) => id.Value;

    static bool IsValid(int value) => value > 0;

    public class DefaultJsonConverter : JsonConverter<HouseholdId>
    {
        public override HouseholdId Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            if (
                reader.TokenType == JsonTokenType.Number
                && reader.TryGetInt16(out var value)
                && HouseholdId.TryParse(value, out var id)
            )
                return id;
            throw new JsonException($"Expected number, found {reader.TokenType}");
        }

        public override void Write(
            Utf8JsonWriter writer,
            HouseholdId value,
            JsonSerializerOptions options
        )
        {
            writer.WriteNumberValue(value.Value);
        }
    }
}
