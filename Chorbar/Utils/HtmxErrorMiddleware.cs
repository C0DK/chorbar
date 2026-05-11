using Chorbar.Model;
using Chorbar.Templates;

namespace Chorbar.Utils;

public class HtmxErrorMiddleware(RequestDelegate next, ILogger logger)
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
        catch (HouseholdNotFoundException)
        {
            await new PageResult(
                new UnhandledErrorPage(
                    message: "Household not found",
                    "Either it doesnt exist or you don't have access to it. sorry"
                )
            ).ExecuteAsync(context);
        }
        catch (NotMemberOfHouseholdException)
        {
            await new PageResult(
                new UnhandledErrorPage(
                    message: "Household not found",
                    "Either it doesnt exist or you don't have access to it. sorry"
                )
            ).ExecuteAsync(context);
        }
#pragma warning disable CA1031
        catch (Exception exception)
#pragma warning restore CA1031
        {
            if (context.RequestAborted.IsCancellationRequested)
                return;
            logger.Error(exception, "Unhandled exception.");

            await new PageResult(
                new UnhandledErrorPage(
                    message: "An unhandled exception occured!",
                    "An unexpected error occured."
                //exception.Message
                )
            ).ExecuteAsync(context);
        }
    }

    public static void Use(IApplicationBuilder builder) =>
        builder.UseMiddleware<HtmxErrorMiddleware>();
}
