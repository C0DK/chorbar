using Chorbar.Templates;
namespace Chorbar.Utils;

public class ModalResult(string content) : IResult
{
    public async Task ExecuteAsync(HttpContext context)
    {
        var response = context.Response;
        var headers = context.Request.Headers;
        response.Headers.Append("Cache-Control", "no-cache");
        response.Headers.Append("HX-Reswap", "none");
        response.Headers.Append("Vary", "HX-Request, HX-Trigger-Name");
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/html";

        await response.WriteAsync(new Modal(content: content));
    }
}
