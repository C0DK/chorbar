using System.Text.Json;
using System.Text.Json.Serialization;
using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

[Authorize]
[Route("household/")]
public class HouseholdsController(HouseholdStore store, IIdentityProvider identityProvider)
    : Controller
{
    [HttpGet("")]
    public async Task<IResult> List(CancellationToken cancellationToken)
    {
        var households = await store.List(cancellationToken).ToArrayAsync(cancellationToken);
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
                        ?? (c.Value.History.IsEmpty ? c.Value.Created : c.Value.History.Last())
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
        return new PageResult(
            ViewHelpers.EditPage(household, identityProvider.GetIdentity(), BaseUrl()),
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
        return new PageResult(
            ViewHelpers.EditPage(household, identityProvider.GetIdentity(), BaseUrl()),
            household.Name
        );
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
            return new PageResult(
                ViewHelpers.EditPage(household, identityProvider.GetIdentity(), BaseUrl()),
                household.Name
            );

        household = await store.Write(householdId, new Rename(name), cancellationToken);
        return new PageResult(
            ViewHelpers.EditPage(household, identityProvider.GetIdentity(), BaseUrl()),
            household.Name
        );
    }

    [HttpGet("{householdId:int}/export.json")]
    public async Task<IResult> ExportJson(
        HouseholdId householdId,
        CancellationToken cancellationToken
    )
    {
        var household = await store.Read(householdId, cancellationToken);
        var export = new
        {
            id = household.Id.Value,
            name = household.Name,
            creator = household.Creator.Value,
            members = household.Members.Select(m => m.Value).OrderBy(m => m).ToArray(),
            chores = household
                .Chores.OrderBy(kv => kv.Key)
                .Select(kv => new
                {
                    name = kv.Key,
                    created = kv.Value.Created,
                    completions = kv.Value.History.OrderBy(t => t).ToArray(),
                    goal = kv.Value.Goal is { } g
                        ? new { every = g.Numerator, unit = g.Unit.ToString().ToLowerInvariant() }
                        : (object?)null,
                })
                .ToArray(),
            exportedAt = DateTimeOffset.UtcNow,
        };

        var json = JsonSerializer.Serialize(export, exportOptions);
        var filename = $"household-{household.Name.ToLowerInvariant().Replace(' ', '-')}.json";
        Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{filename}\"");
        return Results.Text(json, "application/json");
    }

    private JsonSerializerOptions exportOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [HttpPost("{householdId:int}/invite")]
    public async Task<IResult> Invite(
        HouseholdId householdId,
        [FromForm] Email email,
        CancellationToken cancellationToken
    )
    {
        var household = await store.Write(householdId, new AddMember(email), cancellationToken);
        return new PageResult(
            ViewHelpers.EditPage(household, identityProvider.GetIdentity(), BaseUrl()),
            household.Name
        );
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
        return new PageResult(
            ViewHelpers.EditPage(household, currentUser, BaseUrl()),
            household.Name
        );
    }

    [HttpPost("{householdId:int}/ical/generate")]
    public async Task<IResult> GenerateIcal(
        HouseholdId householdId,
        CancellationToken cancellationToken
    )
    {
        var token = Guid.NewGuid().ToString("N");
        var household = await store.Write(
            householdId,
            new GenerateIcalToken(token),
            cancellationToken
        );
        return new PageResult(
            ViewHelpers.EditPage(household, identityProvider.GetIdentity(), BaseUrl()),
            household.Name
        );
    }

    [HttpPost("{householdId:int}/delete")]
    public async Task<IResult> Delete(HouseholdId householdId, CancellationToken cancellationToken)
    {
        await store.Delete(householdId, cancellationToken);
        return new HxRedirectResult("/household/");
    }

    private string BaseUrl() => $"{Request.Scheme}://{Request.Host}";
}
