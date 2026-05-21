using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Chorbar;
using Chorbar.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Chorbar.Tests.Integration;

public class EndpointTests
{
    private ChorbarWebApplicationFactory _factory = null!;

    [SetUp]
    public async Task SetUp()
    {
        _factory = new ChorbarWebApplicationFactory();
        await using var conn = await DatabaseFixture.DataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "TRUNCATE household_event, signin_otp",
            conn
        );
        await cmd.ExecuteNonQueryAsync();
    }

    [TearDown]
    public void TearDown() => _factory.Dispose();

    [Test, CancelAfter(10_000)]
    public async Task LoginPageRenders(CancellationToken cancellationToken)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/auth/", cancellationToken);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test, CancelAfter(10_000)]
    public async Task UnauthenticatedHouseholdListRedirectsToLogin(
        CancellationToken cancellationToken
    )
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var response = await client.GetAsync("/household/", cancellationToken);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Found));
        Assert.That(response.Headers.Location?.ToString(), Does.Contain("/auth/"));
    }

    [Test, CancelAfter(10_000)]
    public async Task HouseholdPageRendersWhenAuthenticated(CancellationToken cancellationToken)
    {
        var identity = new Email("test@example.com");
        await using var conn = await DatabaseFixture.DataSource.OpenConnectionAsync();
        var store = new HouseholdStore(conn, new StaticIdentityProvider(identity));
        var householdId = await store.New("Test Household", cancellationToken);

        var client = _factory.CreateAuthenticatedClient(identity.Value);
        var response = await client.GetAsync(
            $"/household/{householdId.Value}/",
            cancellationToken
        );
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}

sealed class ChorbarWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<NpgsqlDataSource>();
            services.AddSingleton(_ => NpgsqlDataSource.Create(DatabaseFixture.ConnectionString));
            services.RemoveAll<NpgsqlConnection>();
            services.AddTransient(s =>
                s.GetRequiredService<NpgsqlDataSource>().OpenConnection()
            );
        });
    }

    public HttpClient CreateAuthenticatedClient(string email = "test@example.com")
    {
        return WithWebHostBuilder(b =>
            b.ConfigureTestServices(services =>
            {
                services
                    .AddAuthentication("Test")
                    .AddScheme<TestAuthHandlerOptions, TestAuthHandler>(
                        "Test",
                        opts => opts.Email = email
                    );
                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = "Test";
                    opts.DefaultChallengeScheme = "Test";
                });
            })
        ).CreateClient();
    }
}

sealed class TestAuthHandlerOptions : AuthenticationSchemeOptions
{
    public string Email { get; set; } = "test@example.com";
}

sealed class TestAuthHandler(
    IOptionsMonitor<TestAuthHandlerOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<TestAuthHandlerOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[] { new Claim(ClaimTypes.Email, Options.Email) };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
