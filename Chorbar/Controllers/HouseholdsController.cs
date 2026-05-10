using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

[Route("household/")]
public class HouseholdsController(HouseholdStore store) : Controller
{
    [HttpGet("")]
    public async Task<IResult> List(CancellationToken cancellationToken)
    {
        var households = await store.List(cancellationToken).ToArrayAsync();
        var selector = new HouseholdSelector(
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
    public async Task<IResult> Create(
        [FromForm] string name,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrEmpty(name))
            return new PageResult(new NewHousehold());

        var id = await store.New(name, cancellationToken);
        return new HxRedirectResult($"/household/{id.Value}/");
    }

    [HttpGet("{householdId:int}/")]
    public async Task<IResult> View(HouseholdId householdId, CancellationToken cancellationToken)
    {
        var household = await store.Read(householdId, cancellationToken);
        HttpContext.UpdateHousehold(household);
        return new PageResult(
            new HouseholdPage(
                chores: household
                    .Chores.OrderBy(c =>
                        c.Value.Deadline()
                        ?? (c.Value.History.Any() ? c.Value.History.Last() : c.Value.Created)
                    )
                    .Select(ViewHelpers.ChoreCard)
            ),
            household.Name
        );
    }

    [HttpGet("{householdId:int}/edit")]
    public async Task<IResult> EditForm(
        HouseholdId householdId,
        CancellationToken cancellationToken
    )
    {
        var household = await store.Read(householdId, cancellationToken);
        return new PageResult(ViewHelpers.EditPage(household), household.Name);
    }

    [HttpPost("{householdId:int}/edit")]
    public async Task<IResult> Edit(
        HouseholdId householdId,
        [FromForm] string name,
        CancellationToken cancellationToken
    )
    {
        var household = await store.Read(householdId, cancellationToken);
        if (string.IsNullOrEmpty(name))
            return new PageResult(ViewHelpers.EditPage(household), household.Name);

        household = await store.Write(householdId, new Rename(name), cancellationToken);
        return new PageResult(ViewHelpers.EditPage(household), household.Name);
    }

    [HttpPost("{householdId:int}/invite")]
    public async Task<IResult> Invite(
        HouseholdId householdId,
        [FromForm] Email email,
        CancellationToken cancellationToken
    )
    {
        var household = await store.Write(
            householdId,
            new AddMember(email),
            cancellationToken
        );
        return new PageResult(ViewHelpers.EditPage(household), household.Name);
    }

    [HttpPost("{householdId:int}/remove_member")]
    public async Task<IResult> RemoveMember(
        HouseholdId householdId,
        [FromForm] Email email,
        CancellationToken cancellationToken
    )
    {
        var household = await store.Write(
            householdId,
            new RemoveMember(email),
            cancellationToken
        );
        return new PageResult(ViewHelpers.EditPage(household), household.Name);
    }
}
