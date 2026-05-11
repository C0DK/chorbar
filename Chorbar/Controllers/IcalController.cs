using Chorbar.Model;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

[Route("ical/")]
public class IcalController(HouseholdStore store) : Controller
{
    [HttpGet("{householdId:int}/{token}")]
    public async Task<IResult> Subscribe(
        HouseholdId householdId,
        string token,
        CancellationToken cancellationToken
    )
    {
        var household = await store.ReadForIcal(householdId, token, cancellationToken);
        if (household is null)
            return Results.NotFound();

        return Results.Content(IcalBuilder.Build(household), "text/calendar; charset=utf-8");
    }
}
