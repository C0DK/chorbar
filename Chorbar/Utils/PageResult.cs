using Chorbar.Model;
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
        var pageTitle = title is null ? "Chorbar" : $"Chorbar | {title}";
        string householdName = "";
        var authed =
            context.User.Identity?.IsAuthenticated is true
            && context.User.GetEmailOrNull() is not null;
        // TODO: Force redirect if auth state changed!
        if (
            authed
            && context.Request.RouteValues.TryGetValue("householdId", out var householdId)
            && householdId is not null
        )
        {
            // TODO: move to optinoal service registration?
            var household = await context
                .RequestServices.GetRequiredService<HouseholdStore>()
                .Read(HouseholdId.Parse(householdId!.ToString()!), context.RequestAborted);
            householdName = household.Name;
        }

        if (!headers.ContainsKey("HX-Request")) // this also includes boosted
            await response.WriteAsync(
                new Layout(
                    title: pageTitle,
                    content: content,
                    csrfToken: tokenSet.RequestToken!,
                    inHousehold: !string.IsNullOrWhiteSpace(householdName),
                    householdName: householdName,
                    authed: authed
                )
            );
        else
        {
            // TODO: check if auth has changed, and if yes, also update nav!
            response.Headers["HX-Retarget"] = "main";
            response.Headers["HX-Reswap"] = "innerHTML transition:true";
            await response.WriteAsync(
                @$"
<title>{pageTitle}</title>
{content.Trim()}
            "
            );
        }
    }
}
