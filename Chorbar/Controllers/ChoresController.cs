using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

[Authorize]
[Route("household/{householdId:int}/chore/")]
public class ChoresController(HouseholdStore store) : Controller
{
    [HttpGet("")]
    public async Task<IResult> Card(
        HouseholdId householdId,
        [FromQuery] string label,
        CancellationToken cancellationToken
    )
    {
        var household = await store.Read(householdId, cancellationToken);
        var chore = household.Chores.GetValueOrDefault(label);
        if (chore is null)
            return Results.NotFound();

        return new PartialResult(ViewHelpers.ChoreCard(label, chore));
    }

    [HttpGet("details")]
    public async Task<IResult> Details(
        HouseholdId householdId,
        [FromQuery] string label,
        CancellationToken cancellationToken
    )
    {
        var household = await store.Read(householdId, cancellationToken);
        var chore = household.Chores.GetValueOrDefault(label);
        if (chore is null)
            return Results.NotFound();

        return new PartialResult(ViewHelpers.ChoreInfo(label, chore));
    }

    [HttpPost("goal")]
    public async Task<IResult> Goal(
        HouseholdId householdId,
        [FromForm] string label,
        [FromForm] DateUnit unit,
        [FromForm] int numerator,
        CancellationToken cancellationToken
    )
    {
        var household = await store.Write(
            householdId,
            new SetGoal(label, numerator, unit),
            cancellationToken
        );
        var chore = household.Chores[label];
        return new PartialResult(ViewHelpers.ChoreInfo(label, chore));
    }

    [HttpPost("add")]
    public async Task<IResult> Add(
        HouseholdId householdId,
        [FromForm] string label,
        CancellationToken cancellationToken
    )
    {
        var household = await store.Write(
            householdId,
            new AddChore(label),
            cancellationToken
        );
        var chore = household.Chores[label];
        return new PartialResult(ViewHelpers.ChoreCard(label, chore));
    }

    [HttpPost("rename")]
    public async Task<IResult> Rename(
        HouseholdId householdId,
        [FromForm] string oldLabel,
        [FromForm] string newLabel,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(newLabel) || newLabel == oldLabel)
        {
            var current = await store.Read(householdId, cancellationToken);
            var chore = current.Chores.GetValueOrDefault(oldLabel);
            if (chore is null)
                return Results.NotFound();
            return new PartialResult(ViewHelpers.ChoreInfo(oldLabel, chore));
        }

        var household = await store.Write(
            householdId,
            new RenameChore(oldLabel, newLabel),
            cancellationToken
        );
        var renamed = household.Chores[newLabel];
        return new PartialResult(ViewHelpers.ChoreInfo(newLabel, renamed));
    }

    [HttpPost("remove")]
    public async Task<IResult> Remove(
        HouseholdId householdId,
        [FromForm] string label,
        CancellationToken cancellationToken
    )
    {
        var household = await store.Write(
            householdId,
            new RemoveChore(label),
            cancellationToken
        );
        return new PageResult(
            new HouseholdPage(chores: household.Chores.Select(ViewHelpers.ChoreCard)),
            household.Name
        );
    }

    [HttpPost("do")]
    public async Task<IResult> Do(
        HouseholdId householdId,
        [FromForm] string label,
        CancellationToken cancellationToken
    )
    {
        var household = await store.Write(
            householdId,
            new DoChore(label),
            cancellationToken
        );
        var chore = household.Chores[label];
        return new PartialResult(ViewHelpers.ChoreInfo(label, chore));
    }

    [HttpPost("undo")]
    public async Task<IResult> Undo(
        HouseholdId householdId,
        [FromForm] string label,
        [FromForm] DateTimeOffset timestamp,
        CancellationToken cancellationToken
    )
    {
        var household = await store.Write(
            householdId,
            new UndoChore(label, timestamp),
            cancellationToken
        );
        var chore = household.Chores[label];
        return new PartialResult(ViewHelpers.ChoreInfo(label, chore));
    }
}
