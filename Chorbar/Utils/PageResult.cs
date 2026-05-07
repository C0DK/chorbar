using Chorbar.Templates;
using Microsoft.AspNetCore.Antiforgery;

namespace Chorbar.Utils;

public class PageResult(string content, string? title = null) : IResult
{
    public async Task ExecuteAsync(HttpContext context)
    {
        var response = context.Response;
        var headers = context.Request.Headers;
        // caching and htmx is dumb
        if (headers.ContainsKey("HX-Request"))
        {
            response.Headers.Append("Cache-Control", "no-cache");
        }
        // right?
        response.Headers.Append("Vary", "HX-Request, HX-Trigger-Name");
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/html";
        var tokenSet = context
            .RequestServices.GetRequiredService<IAntiforgery>()
            .GetAndStoreTokens(context);
        var pageTitle = title is null ? "Chor.bar — Shared household chore tracking, without the nagging" : $"Chor.bar | {title}";
        var householdName = context.GetHouseholdName();
        var authed =
            context.User.Identity?.IsAuthenticated is true
            && context.User.GetEmailOrNull() is not null;

        // TODO: only if changed
        var nav = new Nav(
            inHousehold: !string.IsNullOrWhiteSpace(householdName),
            householdName: context.GetHouseholdName(),
            authed: authed
        );
        if (!headers.ContainsKey("HX-Request")) // this also includes boosted
            await response.WriteAsync(
                new Layout(
                    nav: nav,
                    title: pageTitle,
                    content: content,
                    csrfToken: tokenSet.RequestToken!
                )
            );
        else
        {
            response.Headers["HX-Retarget"] = "body";
            response.Headers["HX-Reswap"] = "innerHTML transition:true";
            // make this less codey ugly
            await response.WriteAsync(
                $"""
                <title>{pageTitle}</title>
                {nav}
                <main>
                    {content.Trim()}
                </main>
                <div id="modal"></div>
                <footer>
                    Chorbar - A very unfinished product
                </footer>
                """
            );
        }
    }
}
