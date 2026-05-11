using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

[Authorize]
[Route("household/")]
public class HouseholdsController(HouseholdStore store, IIdentityProvider identityProvider) : Controller
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
    public async Task<IResult> Create([FromForm] string name, CancellationToken cancellationToken)
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
                shoppingListEnabled: household.ShoppingListEnabled,
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

    [HttpPost("{householdId:int}/enable_shopping_list")]
    public async Task<IResult> SetShowShoppingList(
        HouseholdId householdId,
        CancellationToken cancellationToken
    )
    {
        var household = await store.Write(
            householdId,
            new EnableShoppingList(Request.Form.GetCheckbox("enabled")),
            cancellationToken
        );
        return new PageResult(ViewHelpers.EditPage(household, identityProvider.GetIdentity()), household.Name);
    }

    [HttpGet("{householdId:int}/edit")]
    public async Task<IResult> EditForm(
        HouseholdId householdId,
        CancellationToken cancellationToken
    )
    {
        var household = await store.Read(householdId, cancellationToken);
        return new PageResult(ViewHelpers.EditPage(household, identityProvider.GetIdentity()), household.Name);
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
            return new PageResult(ViewHelpers.EditPage(household, identityProvider.GetIdentity()), household.Name);

        household = await store.Write(householdId, new Rename(name), cancellationToken);
        return new PageResult(ViewHelpers.EditPage(household, identityProvider.GetIdentity()), household.Name);
    }

    [HttpPost("{householdId:int}/invite")]
    public async Task<IResult> Invite(
        HouseholdId householdId,
        [FromForm] Email email,
        CancellationToken cancellationToken
    )
    {
        var household = await store.Write(householdId, new AddMember(email), cancellationToken);
        return new PageResult(ViewHelpers.EditPage(household, identityProvider.GetIdentity()), household.Name);
    }

    [HttpPost("{householdId:int}/remove_member")]
    public async Task<IResult> RemoveMember(
        HouseholdId householdId,
        [FromForm] Email email,
        CancellationToken cancellationToken
    )
    {
        var currentUser = identityProvider.GetIdentity();
        if (email == currentUser)
            throw new InvalidOperationException("You cannot remove yourself from the household.");

        var household = await store.Write(householdId, new RemoveMember(email), cancellationToken);
        return new PageResult(ViewHelpers.EditPage(household, currentUser), household.Name);
    }
}
