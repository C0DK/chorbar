using Chorbar.Model;

namespace Chorbar.Routes;

public static class IcalRouter
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet(
            "/{householdId:int}/{token}",
            async Task<IResult> (
                HouseholdId householdId,
                string token,
                HouseholdStore store,
                CancellationToken cancellationToken
            ) =>
            {
                var household = await store.ReadForIcal(householdId, token, cancellationToken);
                if (household is null)
                    return Results.NotFound();

                return Results.Content(IcalBuilder.Build(household), "text/calendar; charset=utf-8");
            }
        )
        .AllowAnonymous();
    }
}
