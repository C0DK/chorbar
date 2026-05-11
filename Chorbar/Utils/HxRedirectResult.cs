namespace Chorbar.Utils;

public class HxRedirectResult(string route) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var headers = httpContext.Request.Headers;
        if (!headers.ContainsKey("HX-Request"))
            await Results.Redirect(route).ExecuteAsync(httpContext);
        else
        {
            httpContext.Response.Headers["Hx-Redirect"] = route;
            httpContext.Response.StatusCode = StatusCodes.Status200OK;
        }
    }
}
