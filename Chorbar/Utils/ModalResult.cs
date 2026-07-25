using Chorbar.Templates;

namespace Chorbar.Utils;

public class ModalResult(string content, string? oob = null, string? pushUrl = null) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        var response = httpContext.Response;
        var currentUrl = GetRelativeUrl(
            httpContext.Request.Headers["HX-Current-URL"].FirstOrDefault()
        );
        if (pushUrl is not null && pushUrl == currentUrl)
            pushUrl = null;

        response.Headers.Append("Cache-Control", "no-cache");
        response.Headers.Append("HX-Reswap", "none");
        response.Headers.Append("Vary", "HX-Request, HX-Trigger-Name");
        response.Headers.Append("HX-Push-Url", pushUrl ?? "false");
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/html";

        var returnUrl = pushUrl is not null ? currentUrl : null;

        // if already modal, then do an inner maybe? .. :)
        return response.WriteAsync(new Modal(content: content, returnUrl: returnUrl) + (oob ?? ""));
    }

    private static string? GetRelativeUrl(string? currentUrl) =>
        currentUrl is not null && Uri.TryCreate(currentUrl, UriKind.Absolute, out var uri)
            ? uri.AbsolutePath
            : currentUrl;
}
