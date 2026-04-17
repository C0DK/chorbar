using Serilog.Events;

public static class EnvironmentVariable
{
    public static bool? GetBool(string key) =>
        GetOrNull(key) is { Length: > 0 } value
            ? value.Equals("true", StringComparison.InvariantCultureIgnoreCase)
            : null;

    public static string GetRequired(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value))
            throw new NullReferenceException($"Environment variable {name} was not set");

        return value;
    }

    public static string? GetOrNull(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value))
            return null;

        return value;
    }

    public static LogEventLevel LogLevel =>
        GetOrNull("LOG_LEVEL") is { } value
            ? Enum.Parse<LogEventLevel>(value, ignoreCase: true)
            : LogEventLevel.Debug;
}
