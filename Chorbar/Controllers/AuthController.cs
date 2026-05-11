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
public class AuthController : Controller
{
    public const string SendPolicy = "auth-send";
    public const string VerifyPolicy = "auth-verify";

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
        var form = Request.Form;
        var returnUrl = form.GetString("returnUrl");
        if (!Email.TryParse(form.GetString("email"), out var email))
            return RenderLoginForm(
                email: email,
                error: "Email is not valid!",
                returnUrl: returnUrl
            );

        var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000);
        await mailer.SendAuthToken(email, code, cancellationToken);

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
            .ToArrayAsync();
        if (matches is not { Length: > 0 })
            return RenderCodeForm(email, persist, "Invalid code!");
        if (DateTimeOffset.UtcNow.Subtract(matches.Single()) > TimeSpan.FromMinutes(10))
            return RenderLoginForm(email: email, error: "Code expired", returnUrl: returnUrl);

        await SignIn(HttpContext, email, persist);
        return new HxRedirectResult(returnUrl ?? "/household/");
    }

    [HttpGet("logout")]
    public IResult Logout([FromQuery] string? returnUrl = null)
    {
        HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.ClearHousehold();
        return Results.Redirect(returnUrl ?? "/");
    }

    public static async ValueTask SignIn(HttpContext context, Email email, bool persist) =>
        await SignIn(context, persist, [new Claim(ClaimTypes.Email, email.Value)]);

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

    private static IResult RenderCodeForm(
        Email email,
        bool persist = false,
        string? returnUrl = null,
        string error = ""
    ) =>
        new PartialResult(
            new OtpCodeField(email: email, error: error, persist: persist, returnUrl: returnUrl)
        );

    private static IResult RenderLoginForm(
        Email? email,
        string error = "",
        string? returnUrl = null
    ) => new PartialResult(new LoginForm(email: email?.Value, error: error, returnUrl: returnUrl));
}
