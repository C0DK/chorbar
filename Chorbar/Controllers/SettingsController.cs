using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

[Authorize]
[Route("settings/")]
public class SettingsController(UserStore userStore) : Controller
{
    [HttpGet("")]
    public async Task<IResult> Page(CancellationToken cancellationToken)
    {
        var settings = await userStore.ReadSettings(cancellationToken);

        return RenderSettingsPage(settings);
    }

    [HttpPost("display_name")]
    public ValueTask<IResult> SetDisplayName(
        [FromForm] string displayName,
        CancellationToken cancellationToken
    ) => Apply(new SetDisplayName(displayName), cancellationToken);

    private async ValueTask<IResult> Apply(
        UserEventPayload payload,
        CancellationToken cancellationToken
    )
    {
        var settings = await userStore.Write(payload, cancellationToken);

        return RenderSettingsPage(settings);
    }

    private IResult RenderSettingsPage(UserSettings settings)
    {
        var page = new SettingsPage(email: settings.Email, displayName: settings.DisplayName);

        if (Request.Headers["HX-Target"].Contains("modal"))
        {
            Response.Headers.Append("HX-Push-Url", "false");
            return new ModalResult(page);
        }

        return new PageResult(page);
    }
}
