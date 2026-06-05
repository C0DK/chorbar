using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

[Authorize]
[Route("settings/")]
public class SettingsController(IHouseholdStore householdStore, UserStore userStore) : Controller
{
    [HttpPost("display_name")]
    public async Task<IResult> SetDisplayName(
        [FromForm] string displayName,
        CancellationToken cancellationToken
    )
    {
        await userStore.Write(new SetDisplayName(displayName), cancellationToken);
        var settings = await userStore.ReadSettings(cancellationToken);
        var households = await householdStore
            .List(cancellationToken)
            .ToArrayAsync(cancellationToken);
        return new PartialResult(
            new Menu(
                email: settings.Email,
                displayName: settings.DisplayName,
                households: households.Select(h => new HouseholdSelectorOption(
                    id: h.Id.ToString() ?? "id",
                    name: h.Name ?? "name"
                ))
            )
        );
    }
}
