namespace Chorbar.Routes;

public static class RootRouter
{
    public static void Map(WebApplication app)
    {
        app.MapGet(
            "/",
            async Task<IResult> (
                HttpContext context,
                HouseholdStore store,
                CancellationToken cancellationToken
            ) =>
            {
                return Results.Redirect("/household/");
            }
        );
        HouseholdRouter.Map(app.MapGroup("/household/"));
    }
}

public static class AuthRouter
{
    public static void Map(WebApplication app)
    {
        app.MapGet(
            "/",
            async Task<IResult> (
                HttpContext context,
                HouseholdStore store,
                CancellationToken cancellationToken
            ) =>
            {
                return Results.Redirect("/household/");
            }
        );
    }
}
