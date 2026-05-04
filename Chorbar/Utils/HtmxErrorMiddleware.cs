using Chorbar.Model;
using Chorbar.Templates;

namespace Chorbar.Utils;

public class HtmxErrorMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Call the next delegate/middleware in the pipeline.
            await next(context);
        }
        catch (LoginRequiredException)
        {
            var returnUrl = context.Request.Path + context.Request.QueryString;
            await new HxRedirectResult(
                $"/auth/?returnUrl={Uri.EscapeDataString(returnUrl)}"
            ).ExecuteAsync(context);
        }
        catch (Exception exception)
        {
            if (context.RequestAborted.IsCancellationRequested)
                return;
            await new PageResult(
                new UnhandledErrorPage(
                    message: "An unhandled exception occured!",
                    exception.Message
                )
            ).ExecuteAsync(context);
        }
    }

    public static void Use(IApplicationBuilder builder) =>
        builder.UseMiddleware<HtmxErrorMiddleware>();
}


