global using ILogger = Serilog.ILogger;
using System.Text.Json;
using Chorbar.Routes;
using Microsoft.AspNetCore.Authentication.Cookies;
using Npgsql;
using Serilog;

Log.Logger = Logging.CreateConfiguration().CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddSerilog();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(Log.Logger);
builder.Services.AddHttpClient();
builder.Services.AddTransient<NpgsqlConnection>(s =>
    s.GetRequiredService<NpgsqlDataSource>().OpenConnection()
);
builder.Services.AddSingleton<NpgsqlDataSource>(_ =>
    NpgsqlDataSource.Create("Host=127.0.0.1;Username=postgres;Database=postgres")
);
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.SuppressXFrameOptionsHeader = false;
});
builder
    .Services.AddAuthorization()
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // TODO: handle never Remember Me and stuff
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
        options.SlidingExpiration = true;
        // TODO: add
        options.AccessDeniedPath = "/forbidden/";
        options.LoginPath = "/auth"; // TODO: redirect doesnt fully work with htmx!
        options.LogoutPath = "/auth/logout";
    });

var app = builder.Build();

app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();
app.UseSession();
app.UseSerilogRequestLogging();
RootRouter.Map(app);

app.Run();
