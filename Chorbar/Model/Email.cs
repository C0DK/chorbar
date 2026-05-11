using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Strongbars.Abstractions;

namespace Chorbar.Model;

// TODO: remove constructor so we always lower it?
[JsonConverter(typeof(Email.DefaultJsonConverter))]
public readonly record struct Email(string Value)
{
    public override string ToString() => Value;

    public static Email Parse(string value)
    {
        if (TryParse(value, out var email))
            return email;

        throw new InvalidOperationException($"Email '{value}' is not valid!");
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out Email output)
    {
        if (value is not { Length: > 0 } || !IsValidEmail(value))
        {
            output = default;
            return false;
        }
        output = new(value.ToLowerInvariant());
        return true;
    }

    public static implicit operator string(Email email) => email.ToString();

    public static implicit operator TemplateArgument(Email value) => value.ToString();

    static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
        if (!new EmailAddressAttribute().IsValid(email))
            return false;
        var at = email.LastIndexOf('@');
        var domain = email[(at + 1)..];
        return domain.Contains('.', StringComparison.Ordinal)
            && !domain.EndsWith('.')
            && !domain.StartsWith('.');
    }

    public class DefaultJsonConverter : JsonConverter<Email>
    {
        public override Email Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            if (
                reader.TokenType == JsonTokenType.String
                && reader.GetString() is { } value
                && Email.TryParse(value, out var email)
            )
            {
                return email;
            }
            throw new JsonException($"Expected string, found {reader.TokenType}");
        }

        public override void Write(
            Utf8JsonWriter writer,
            Email value,
            JsonSerializerOptions options
        ) => writer.WriteStringValue(value.Value);
    }
}
