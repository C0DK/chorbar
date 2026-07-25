using Chorbar.Templates;

namespace Chorbar.Utils;

public class ModalResult(string content, string? oob = null, string? pushUrl = null) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        var response = httpContext.Response;
        response.Headers.Append("Cache-Control", "no-cache");
        response.Headers.Append("HX-Reswap", "none");
        response.Headers.Append("Vary", "HX-Request, HX-Trigger-Name");
        response.Headers.Append("HX-Push-Url", pushUrl ?? "false");
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/html";

        // TODO: what do if not htmx?? i guess never the case
        var returnUrl = pushUrl is not null
            ? httpContext.Request.Headers["HX-Current-URL"].First()
            : null;

        // if already modal, then do an inner maybe? .. :)
        return response.WriteAsync(new Modal(content: content, returnUrl: returnUrl) + (oob ?? ""));
    }
}
