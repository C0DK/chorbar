using System.Text.Json;
using System.Text.Json.Serialization;
using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

[Route("household/{householdId:int}/")]
public class HouseholdController(IHouseholdStore store, IIdentityProvider identityProvider)
    : SpecificHouseholdControllerBase(store)
{
    private readonly JsonSerializerOptions exportOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [HttpGet("")]
    public async Task<IResult> View(CancellationToken cancellationToken)
    {
        var household = await Get(cancellationToken);
        HttpContext.UpdateHousehold(household);
        return new PageResult(
            new HouseholdPage(
                shoppingListEnabled: household.ShoppingListEnabled,
                todoListEnabled: household.TodoListEnabled,
                chores: household
                    .Chores
                    .Select(ViewHelpers.ChoreCard)
            ),
            household.Name
        );
    }

    [HttpPost("enable_shopping_list")]
    public async Task<IResult> SetShowShoppingList(CancellationToken cancellationToken)
    {
        var household = await Write(
            new EnableShoppingList(Request.Form.GetCheckbox("enabled")),
            cancellationToken
        );
        return EditPage(household);
    }

    [HttpPost("enable_todo_list")]
    public async Task<IResult> SetEnableTodo(CancellationToken cancellationToken)
    {
        var household = await Write(
            new EnableTodoList(Request.Form.GetCheckbox("enabled")),
            cancellationToken
        );
        return EditPage(household);
    }

    [HttpGet("edit")]
    public async Task<IResult> EditForm(CancellationToken cancellationToken) =>
        EditPage(await Get(cancellationToken));

    [HttpPost("edit")]
    public async Task<IResult> Edit([FromForm] string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(name))
            return EditPage(await Get(cancellationToken));

        return EditPage(await Write(new Rename(name), cancellationToken));
    }

    [HttpGet("export.json")]
    public async Task<IResult> ExportJson(CancellationToken cancellationToken)
    {
        var household = await Get(cancellationToken);
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

    [HttpPost("invite")]
    public async Task<IResult> Invite(
        [FromForm] Email email,
        CancellationToken cancellationToken
    ) => EditPage(await Write(new AddMember(email), cancellationToken));

    [HttpPost("remove_member")]
    public async Task<IResult> RemoveMember(
        [FromForm] Email email,
        CancellationToken cancellationToken
    )
    {
        if (email == identityProvider.GetIdentity())
            throw new InvalidOperationException("You cannot remove yourself from the household.");

        return EditPage(await Write(new RemoveMember(email), cancellationToken));
    }

    [HttpPost("ical/generate")]
    public async Task<IResult> GenerateIcal(CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString("N");
        return EditPage(await Write(new GenerateIcalToken(token), cancellationToken));
    }

    [HttpPost("delete")]
    public async Task<IResult> Delete(CancellationToken cancellationToken)
    {
        await Store.Delete(HouseholdId, cancellationToken);
        return new HxRedirectResult("/household/");
    }

    private IResult EditPage(Household household)
    {
        var page = ViewHelpers.EditPage(household, identityProvider.GetIdentity(), BaseUrl());
        if (Request.Headers["HX-Target"].Contains("modal"))
        {
            Response.Headers.Append("HX-Push-Url", "false");
            return new ModalResult(page);
        }
        return new PageResult(page, household.Name);
    }

    private string BaseUrl() => $"{Request.Scheme}://{Request.Host}";
}
