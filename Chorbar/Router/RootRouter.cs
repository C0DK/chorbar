using Chorbar.Templates;
using Chorbar.Utils;

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
                return new PageResult(new LandingPage());
            }
        );
        HouseholdRouter.Map(app.MapGroup("/household/"));
        AuthRouter.Map(app.MapGroup("/auth/"));
    }
}
