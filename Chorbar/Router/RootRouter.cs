namespace Chorbar.Routes;

public static class RootRouter
{
    public static void Map(WebApplication app)
    {
        app.MapGet(
            "/",
            (HttpContext context, string? returnUrl = null) =>
            {
                return "Hello world!";
            }
        );
    }
}
