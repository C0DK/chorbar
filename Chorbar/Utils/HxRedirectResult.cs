namespace Chorbar.Utils;

public class HxRedirectResult(string route) : IResult
{
    public async Task ExecuteAsync(HttpContext context)
    {
        var headers = context.Request.Headers;
        if (!headers.ContainsKey("HX-Request"))
            await Results.Redirect(route).ExecuteAsync(context);
        else
        {
            context.Response.Headers["Hx-Redirect"] = route;
            context.Response.StatusCode = StatusCodes.Status200OK;
        }
    }
}

