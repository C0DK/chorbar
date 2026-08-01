using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;

namespace Chorbar.Controllers;

[Route("auth/")]
public class AuthController(AuthMetrics authMetrics) : Controller
{
    public const string SendPolicy = "auth-send";
    public const string VerifyPolicy = "auth-verify";
    public const string ActivitySourceName = "Chorbar.Auth";
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    [HttpGet("")]
    public IResult Index([FromQuery] string? returnUrl = null)
    {
        if (HttpContext.User.Identity?.IsAuthenticated is true)
            return new HxRedirectResult(returnUrl ?? "/");

        return new PageResult(
            new LoginForm(email: null, error: null, returnUrl: returnUrl),
            "Please Sign In"
        );
    }

    [HttpPost("")]
    [EnableRateLimiting(SendPolicy)]
    public async Task<IResult> Send(
        [FromServices] NpgsqlConnection connection,
        [FromServices] IMailSender mailer,
        [FromServices] ILogger logger,
        CancellationToken cancellationToken
    )
    {
        using var activity = ActivitySource.StartActivity("Auth.Send");
        var form = Request.Form;
        var returnUrl = form.GetString("returnUrl");
        if (!Email.TryParse(form.GetString("email"), out var email))
            return RenderLoginForm(
                email: email,
                error: "Email is not valid!",
                returnUrl: returnUrl
            );

        var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000);
        await mailer.SendAuthToken(email, code, LoginUrl(email, code), cancellationToken);
        authMetrics.OtpRequested();

        await connection.ExecuteAsync(
            "INSERT INTO signin_otp(email, code) VALUES($1, $2)",
            p =>
            {
                p.AddWithValue(email.Value);
                p.AddWithValue(code);
            },
            cancellationToken
        );

        return RenderCodeForm(email, returnUrl: returnUrl);
    }

    [HttpPost("code")]
    [EnableRateLimiting(VerifyPolicy)]
    public async Task<IResult> VerifyCode(
        [FromServices] NpgsqlDataSource db,
        CancellationToken cancellationToken
    )
    {
        using var activity = ActivitySource.StartActivity("Auth.VerifyCode");
        var form = Request.Form;
        var returnUrl = form.GetString("returnUrl");
        if (!Email.TryParse(form.GetString("email"), out var email))
            return RenderLoginForm(
                email: email,
                error: "Email is not valid!",
                returnUrl: returnUrl
            );
        var persist = form.GetCheckbox("persist");
        if (form.GetString("code") is not { Length: > 0 } code)
            return RenderCodeForm(email, persist, error: "Invalid code!", returnUrl: returnUrl);

        var outcome = await Verify(db, email, code, cancellationToken);
        if (outcome is VerifyResult.Invalid)
        {
            authMetrics.OtpResult("invalid");
            return RenderCodeForm(email, persist, "Invalid code!");
        }
        if (outcome is VerifyResult.Expired)
        {
            authMetrics.OtpResult("expired");
            return RenderLoginForm(email: email, error: "Code expired", returnUrl: returnUrl);
        }

        authMetrics.OtpResult("ok");
        await SignIn(HttpContext, email, persist);
        return new HxRedirectResult(returnUrl ?? "/household/");
    }

    [HttpGet("login")]
    [EnableRateLimiting(VerifyPolicy)]
    public async Task<IResult> Login(
        [FromServices] NpgsqlDataSource db,
        CancellationToken cancellationToken,
        [FromQuery] string? email = null,
        [FromQuery] string? code = null,
        [FromQuery] string? returnUrl = null
    )
    {
        using var activity = ActivitySource.StartActivity("Auth.Login");
        if (!Email.TryParse(email, out var parsedEmail) || code is not { Length: > 0 })
            return AuthErrorPage("Invalid login link!", email, returnUrl);

        var outcome = await Verify(db, parsedEmail, code, cancellationToken);
        if (outcome is VerifyResult.Invalid)
        {
            authMetrics.OtpResult("invalid");
            return AuthErrorPage("Invalid login link!", parsedEmail.Value, returnUrl);
        }
        if (outcome is VerifyResult.Expired)
        {
            authMetrics.OtpResult("expired");
            return AuthErrorPage("The login link has expired.", parsedEmail.Value, returnUrl);
        }

        authMetrics.OtpResult("ok");
        await SignIn(HttpContext, parsedEmail, persist: false);
        return Results.Redirect(returnUrl ?? "/household/");
    }

    [HttpGet("logout")]
    public IResult Logout([FromQuery] string? returnUrl = null)
    {
        using var activity = ActivitySource.StartActivity("Auth.Logout");
        HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.ClearHousehold();
        return Results.Redirect(returnUrl ?? "/");
    }

    public static ValueTask SignIn(HttpContext context, Email email, bool persist) =>
        SignIn(context, persist, [new Claim(ClaimTypes.Email, email.Value)]);

    private static async ValueTask SignIn(
        HttpContext context,
        bool persist,
        IEnumerable<Claim> claims
    )
    {
        var claimsIdentity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme
        );

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            new AuthenticationProperties { IsPersistent = persist }
        );
    }

    private static PartialResult RenderCodeForm(
        Email email,
        bool persist = false,
        string? returnUrl = null,
        string error = ""
    ) =>
        new PartialResult(
            new OtpCodeField(email: email, error: error, persist: persist, returnUrl: returnUrl)
        );

    private static PartialResult RenderLoginForm(
        Email? email,
        string error = "",
        string? returnUrl = null
    ) => new PartialResult(new LoginForm(email: email?.Value, error: error, returnUrl: returnUrl));

    private static IResult AuthErrorPage(string error, string? email, string? returnUrl) =>
        new PageResult(
            new LoginForm(email: email, error: error, returnUrl: returnUrl),
            "Please Sign In"
        );

    private static string LoginUrl(Email email, int code) =>
        $"{EnvironmentVariable.GetRequired("BASE_URL")}/auth/login"
        + $"?email={Uri.EscapeDataString(email.Value)}"
        + $"&code={code:D6}";

    private enum VerifyResult
    {
        Invalid,
        Expired,
        Ok,
    }

    private static async Task<VerifyResult> Verify(
        NpgsqlDataSource db,
        Email email,
        string code,
        CancellationToken cancellationToken
    )
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            @"
                SELECT created
                FROM signin_otp
                WHERE email = $1 AND code = $2
            ",
            connection
        );
        cmd.Parameters.AddWithValue(email.Value);
        cmd.Parameters.AddWithValue(code);
        var matches = await cmd.ReadAllAsync<DateTime>(
                (reader, cancellationToken) =>
                    reader.GetFieldValueAsync<DateTime>(0, cancellationToken),
                cancellationToken
            )
            .ToArrayAsync(cancellationToken);
        if (matches is not { Length: > 0 })
            return VerifyResult.Invalid;
        if (DateTimeOffset.UtcNow.Subtract(matches.Single()) > TimeSpan.FromMinutes(10))
            return VerifyResult.Expired;
        return VerifyResult.Ok;
    }
}
