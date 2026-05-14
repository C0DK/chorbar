namespace Chorbar.Utils;

public class PartialResult(
    string content,
    Dictionary<string, string>? headers = null,
    bool closeModal = false
) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        var response = httpContext.Response;

        response.Headers.Append("Cache-Control", "no-cache");
        response.Headers.Append("Vary", "HX-Request, HX-Trigger-Name");
        foreach (var header in headers?.ToArray() ?? [])
        {
            response.Headers.Append(header.Key, header.Value);
        }
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/html";
        if (closeModal)
            response.WriteAsync("<div id='modal' hx-swap-oob='outerHTML'></div>");

        return response.WriteAsync(content);
    }
}
