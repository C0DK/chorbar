using System.Security.Claims;
using System.Security.Cryptography;
using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Npgsql;

namespace Chorbar.Routes;

public static class AuthRouter
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/",
            IResult (HttpContext context, string? returnUrl = null) =>
            {
                if (context.User.Identity?.IsAuthenticated is true)
                    return new HxRedirectResult(returnUrl ?? "/");

                return new PageResult(new LoginForm(email: null, error: null), "Please Sign In");
            }
        );
        app.MapPost(
            "/",
            async (
                HttpRequest request,
                NpgsqlConnection connection,
                CancellationToken cancellationToken,
                IMailSender mailer,
                ILogger logger,
                string? returnUrl = null
            ) =>
            {
                if (!Email.TryParse(request.Form.GetString("email"), out var email))
                    return RenderLoginForm(email: email, error: "Email is not valid!");

                var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000);
                await mailer.SendAuthToken(email, code, cancellationToken);

                await using var cmd = new NpgsqlCommand(
                    "INSERT INTO signin_otp(email, code) VALUES($1, $2)",
                    connection
                );
                cmd.Parameters.AddWithValue(email.Value);
                cmd.Parameters.AddWithValue(code);
                await cmd.ExecuteNonQueryAsync();

                return RenderCodeForm(email);
            }
        );

        app.MapPost(
            "/code",
            async (
                HttpContext context,
                NpgsqlDataSource db,
                CancellationToken cancellationToken,
                string? returnUrl = null
            ) =>
            {
                var form = context.Request.Form;
                if (!Email.TryParse(form.GetString("email"), out var email))
                    return RenderLoginForm(email: email, error: "Email is not valid!");
                var persist = form.GetCheckbox("persist");
                if (form.GetString("code") is not { Length: > 0 } code)
                    return RenderCodeForm(email, persist, error: "Invalid code!");

                // TODO: handle never Remember Me and stuff
                await using var connection = await db.OpenConnectionAsync(cancellationToken);

                await using var cmd = new NpgsqlCommand(
                    @"
                    SELECT                      
                      created
                    FROM signin_otp
                    WHERE email = $1 AND code = $2 
                    ",
                    connection
                );
                cmd.Parameters.AddWithValue(email.Value);
                cmd.Parameters.AddWithValue(code);
                var matches = await cmd.ReadAllAsync(
                        reader => reader.GetFieldValue<DateTime>(0),
                        cancellationToken
                    )
                    .ToArrayAsync();
                if (matches is not { Length: > 0 })
                    return RenderCodeForm(email, persist, "Invalid code!");
                if (DateTimeOffset.UtcNow.Subtract(matches.Single()) > TimeSpan.FromMinutes(10))
                    return RenderLoginForm(email: email, error: "Code expired");

                await SignIn(context, email, persist);
                return new HxRedirectResult(returnUrl ?? "/");
            }
        );
        app.MapGet(
            "/logout",
            (HttpContext context, string? returnUrl = null) =>
            {
                context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.Redirect(returnUrl ?? "/");
            }
        // should we check if authed? will it fail if not?
        );
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
        // TODO: gate persistence behind "remember me" checkbox.
        );
    }

    private static IResult RenderCodeForm(Email email, bool persist = false, string error = "") =>
        new PartialResult(new OtpCodeField(email: email, error: error, persist: persist));

    private static IResult RenderLoginForm(Email? email, string error = "") =>
        new PartialResult(new LoginForm(email: email?.Value, error: error));
}
