using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

[Authorize]
[Route("household/")]
public class MenuController(IHouseholdStore householdStore) : Controller
{
    [HttpGet("")]
    public async Task<IResult> List(CancellationToken cancellationToken)
    {
        var households = await householdStore.List(cancellationToken).ToArrayAsync(cancellationToken);
        var selector = new Menu(
            households: households.Select(h => new HouseholdSelectorOption(
                id: h.Id.ToString() ?? "id",
                name: h.Name ?? "name"
            ))
        );

        if (Request.Headers["HX-Target"].Contains("modal"))
        {
            Response.Headers.Append("HX-Push-Url", "false");
            return new ModalResult(selector);
        }

        return new PageResult(selector);
    }

    [HttpGet("new")]
    public IResult NewForm() => new PageResult(new NewHousehold());

    [HttpPost("new")]
    public async Task<IResult> Create([FromForm] string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(name))
            return new PageResult(new NewHousehold());

        var id = await householdStore.Create(name, cancellationToken);
        return new HxRedirectResult($"/household/{id.Value}/");
    }
}
