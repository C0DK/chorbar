global using ILogger = Serilog.ILogger;
using Chorbar;
using Chorbar.Model;
using Chorbar.Routes;
using Chorbar.Utils;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;
using Serilog;

Log.Logger = Logging.CreateConfiguration().CreateLogger();

var builder = WebApplication.CreateBuilder(args);

var keyRingDir = EnvironmentVariable.GetOrNull("DATA_PROTECTION_KEY_DIR")
                 ?? "/var/lib/chorbar/keys";
Directory.CreateDirectory(keyRingDir);
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyRingDir))
    .SetApplicationName("chorbar");

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder
    .Services.AddAuthorization()
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.AccessDeniedPath = "/forbidden/";
        options.LoginPath = "/auth"; // TODO: redirect doesnt fully work with htmx!
        options.LogoutPath = "/auth/logout";
    });
builder.Services.AddSerilog();
builder.Services.AddHttpContextAccessor();
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
builder.Services.AddSingleton<NpgsqlDataSource>(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.SuppressXFrameOptionsHeader = false;
});
var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseStaticFiles();
app.UseSession();
app.UseSerilogRequestLogging();
RootRouter.Map(app);
HtmxErrorMiddleware.Use(app);

app.Run();
