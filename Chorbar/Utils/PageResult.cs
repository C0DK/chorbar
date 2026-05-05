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
        var pageTitle = title is null ? "Chor.bar" : $"Chor.bar | {title}";
        var email =
            context.User.Identity?.IsAuthenticated is true
                ? context.User.GetEmailOrNull()?.Value
                : null;
        string? routeHouseholdId = null;
        if (
            context.Request.RouteValues.TryGetValue("householdId", out var hh)
            && hh is not null
        )
            routeHouseholdId = hh.ToString();
        var navState = email is null && routeHouseholdId is null
            ? ""
            : $"{email}|{routeHouseholdId}";

        var isHtmx = headers.ContainsKey("HX-Request");
        var navChanged = !isHtmx || headers["X-Nav-State"].ToString() != navState;

        string nav = "";
        if (navChanged)
        {
            string householdName = "";
            if (email is not null && routeHouseholdId is not null)
            {
                var household = await context
                    .RequestServices.GetRequiredService<HouseholdStore>()
                    .Read(HouseholdId.Parse(routeHouseholdId), context.RequestAborted);
                householdName = household.Name;
            }
            nav = new Nav(
                inHousehold: !string.IsNullOrWhiteSpace(householdName),
                householdName: householdName,
                authed: email is not null,
                oob: isHtmx,
                state: navState
            );
        }

        if (!isHtmx) // this also includes boosted
            await response.WriteAsync(
                new Layout(
                    title: pageTitle,
                    content: content,
                    csrfToken: tokenSet.RequestToken!,
                    nav: nav
                )
            );
        else
        {
            response.Headers["HX-Retarget"] = "main";
            response.Headers["HX-Reswap"] = "innerHTML transition:true";
            await response.WriteAsync(
                @$"
<title>{pageTitle}</title>
{nav}
{content.Trim()}
            "
            );
        }
    }
}
