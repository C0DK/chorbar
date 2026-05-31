using Chorbar.Templates;

namespace Chorbar.Utils;

public class ModalResult(string content, string? oob = null) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        var response = httpContext.Response;
        response.Headers.Append("Cache-Control", "no-cache");
        response.Headers.Append("HX-Reswap", "none");
        response.Headers.Append("Vary", "HX-Request, HX-Trigger-Name");
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/html";

        // if already modal, then do an inner maybe? .. :)
        return response.WriteAsync(new Modal(content: content) + (oob ?? ""));
    }
}
