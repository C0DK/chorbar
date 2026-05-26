global using ILogger = Serilog.ILogger;
using System.Threading.RateLimiting;
using Chorbar;
using Chorbar.Controllers;
using Chorbar.Model;
using Chorbar.Utils;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Serilog;

Log.Logger = Logging.CreateConfiguration().CreateLogger();

var builder = WebApplication.CreateBuilder(args);

var cookieSecurePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

builder.Services.AddDataProtection().SetApplicationName("chorbar");
builder.Services.AddSingleton<IXmlRepository, PgXmlRepository>();
builder
    .Services.AddOptions<KeyManagementOptions>()
    .Configure<IXmlRepository>((opts, repo) => opts.XmlRepository = repo);

builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
});

builder
    .Services.AddAuthorization()
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.AccessDeniedPath = "/forbidden/";
        options.LoginPath = "/auth/"; // TODO: redirect doesnt fully work with htmx!
        options.LogoutPath = "/auth/logout";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = cookieSecurePolicy;
    });
builder.Services.AddControllers();
builder.Services.AddSerilog();

var otlpEndpoint = EnvironmentVariable.GetOrNull("OTEL_EXPORTER_OTLP_ENDPOINT");
builder
    .Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("chorbar"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation(opts =>
        {
            opts.EnrichWithHttpResponse = (activity, response) =>
            {
                if (response.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.ISessionFeature>() is not null)
                    activity.SetTag("session.id", response.HttpContext.Session.Id);
            };
        });
        tracing.AddNpgsql();
        tracing.AddSource(HouseholdStore.ActivitySourceName);
        tracing.AddSource(AuthController.ActivitySourceName);
        if (otlpEndpoint is not null)
            tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<StaticFileVersion>();
builder.Services.AddSingleton(Log.Logger);
builder.Services.AddTransient<IIdentityProvider, ClaimsBasedIdentityProvider>();
builder.Services.AddTransient<HouseholdStore>();
var brevoApiClient = EnvironmentVariable.GetOrNull("BREVO_API_KEY");
if (brevoApiClient is not null)
{
    builder.Services.AddTransient<IMailSender, BrevoClient>(s => new BrevoClient(
        s.GetRequiredService<IHttpClientFactory>().CreateClient(),
        brevoApiClient,
        s.GetRequiredService<ILogger>()
    ));
}
else
{
    Log.Logger.Warning("Configuring mailer to logging");
    builder.Services.AddTransient<IMailSender, LogMailer>();
}
builder.Services.AddTransient<HouseholdStore>();
builder.Services.AddHttpClient();
builder.Services.AddTransient<NpgsqlConnection>(s =>
    s.GetRequiredService<NpgsqlDataSource>().OpenConnection()
);
var connectionString =
    EnvironmentVariable.GetOrNull("DB_CONNECTION_STRING")
    ?? "Host=127.0.0.1;Username=postgres;Database=chorbar";
Log.Logger.Information("Using postgres via {conn}", connectionString);
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.EnableTracing();
builder.Services.AddSingleton<NpgsqlDataSource>(_ => dataSourceBuilder.Build());
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.SuppressXFrameOptionsHeader = false;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(
        AuthController.SendPolicy,
        ClientPartition(permitLimit: 5, window: TimeSpan.FromMinutes(15))
    );

    options.AddPolicy(
        AuthController.VerifyPolicy,
        ClientPartition(permitLimit: 10, window: TimeSpan.FromMinutes(15))
    );
});

static Func<HttpContext, RateLimitPartition<string>> ClientPartition(
    int permitLimit,
    TimeSpan window
) =>
    httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
            }
        );
var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseStaticFiles(
    new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
            ctx.Context.Response.Headers.Append(
                "Cache-Control",
                "public, max-age=31536000, immutable"
            ),
    }
);
app.UseSession();
app.UseRateLimiter();
app.UseSerilogRequestLogging();
PageViewMetricsMiddleware.Use(app);
app.MapMetrics();
app.MapControllers();
HtmxErrorMiddleware.Use(app);

app.Run();
