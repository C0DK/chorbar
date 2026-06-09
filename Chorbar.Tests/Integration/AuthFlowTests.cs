using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace Chorbar.Tests.Integration;

[NonParallelizable]
public class AuthFlowTests
{
    private AppFixture _fixture = null!;
    private HttpClient _httpClient = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _fixture = new AppFixture();
    }

    [SetUp]
    public async Task SetUp()
    {
        _httpClient = _fixture.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        _fixture.FakeMailer.Reset();

        await using var conn = await DatabaseFixture.DataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("TRUNCATE signin_otp", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _fixture.Dispose();
    }

    [Test, CancelAfter(10_000)]
    public async Task HappyPath_LoginWithValidOtp(CancellationToken cancellationToken)
    {
        var client = new HtmxClient(_httpClient);

        // 1. GET /auth/ -> full page with login form
        var doc = await client.GetHtmlDoc("/auth/", cancellationToken);

        var form = doc.QuerySelector("form[hx-post='/auth/']");
        Assert.That(form, Is.Not.Null, "Expected login form");

        // 2. POST /auth/ with email -> OTP form partial
        var postDoc = await client.PostFormAndParse(
            "/auth/",
            [("email", "test@example.com"), ("returnUrl", "")],
            cancellationToken
        );

        var otpForm = postDoc.QuerySelector("form[hx-post='/auth/code']");
        Assert.That(otpForm, Is.Not.Null, "Expected OTP code form");
        Assert.That(postDoc.Body?.InnerHtml, Does.Contain("test@example.com"));

        var code = _fixture.FakeMailer.LastCode;
        Assert.That(code, Is.Not.Null, "Expected OTP to be generated");

        // 3. POST /auth/code with correct OTP -> HX-Redirect
        using var verifyResponse = await client.PostForm(
            "/auth/code",
            [("email", "test@example.com"), ("code", code!.Value.ToString())],
            cancellationToken
        );

        Assert.That(verifyResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(verifyResponse.Headers.Contains("Hx-Redirect"), Is.True);
        Assert.That(
            verifyResponse.Headers.GetValues("Hx-Redirect").Single(),
            Is.EqualTo("/household/")
        );

        // 4. GET /household/ -> should be authenticated
        var authedDoc = await client.GetHtmlDoc("/household/", cancellationToken);
        Assert.That(
            authedDoc.Body?.InnerHtml,
            Does.Not.Contain("Please Sign In"),
            "Should be authenticated"
        );
    }

    [Test, CancelAfter(10_000)]
    public async Task UnhappyPath_WrongOtpShowsError(CancellationToken cancellationToken)
    {
        var client = new HtmxClient(_httpClient);

        // 1. GET /auth/
        var doc = await client.GetHtmlDoc("/auth/", cancellationToken);

        // 2. POST /auth/ -> OTP form
        await client.PostForm("/auth/", [("email", "wrong@example.com")], cancellationToken);

        // 3. POST /auth/code with wrong OTP -> error partial
        var verifyDoc = await client.PostFormAndParse(
            "/auth/code",
            [("email", "wrong@example.com"), ("code", "000000")],
            cancellationToken
        );

        Assert.That(verifyDoc.Body?.InnerHtml, Does.Contain("Invalid code!"));
        Assert.That(verifyDoc.Body?.InnerHtml, Does.Contain("wrong@example.com"));
    }
}
