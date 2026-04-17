using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.SystemConsole.Themes;

public static class Logging
{
    public static LoggerConfiguration CreateConfiguration()
    {
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(EnvironmentVariable.LogLevel)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Server.Kestrel", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Watch", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
            .MinimumLevel.Override(
                "Microsoft.AspNetCore.StaticFiles.StaticFileMiddleware",
                LogEventLevel.Warning
            )
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp} {Level:u3} {SourceContext}] {Message:lj}{NewLine} [{Properties:j}]{NewLine}{Exception}",
                theme: AnsiConsoleTheme.Code,
                applyThemeToRedirectedOutput: true
            )
            .WriteTo.Console(new RenderedCompactJsonFormatter());

        return loggerConfig;
    }
}
