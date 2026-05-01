namespace Chorbar.Utils;

public class PartialResult(string content, Dictionary<string, string>? headers = null) : IResult
{
    public async Task ExecuteAsync(HttpContext context)
    {
        var response = context.Response;

        response.Headers.Append("Cache-Control", "no-cache");
        response.Headers.Append("Vary", "HX-Request, HX-Trigger-Name");
        foreach (var header in headers?.ToArray() ?? [])
        {
            response.Headers.Append(header.Key, header.Value);
        }
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/html";

        await response.WriteAsync(content);
    }
}
