using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

[Authorize]
[Route("household/{householdId:int}/chore/")]
public class ChoresController(HouseholdStore store) : SpecificHouseholdControllerBase(store)
{
    [HttpGet("")]
    public async Task<IResult> Card([FromQuery] string label, CancellationToken cancellationToken)
    {
        var household = await Get(cancellationToken);
        var chore = household.Chores.GetValueOrDefault(label);
        if (chore is null)
            return Results.NotFound();

        return new PartialResult(ViewHelpers.ChoreCard(label, chore));
    }

    [HttpGet("edit")]
    public async Task<IResult> Details(
        [FromQuery] string label,
        CancellationToken cancellationToken
    )
    {
        var household = await Get(cancellationToken);
        var chore = household.Chores.GetValueOrDefault(label);
        if (chore is null)
            return Results.NotFound();

        return new ModalResult(ViewHelpers.EditChore(label, chore));
    }

    // todo: return modal + oob
    [HttpPost("goal")]
    public async Task<IResult> Goal(
        [FromForm] string label,
        [FromForm] DateUnit unit,
        [FromForm] int numerator,
        CancellationToken cancellationToken
    )
    {
        var household = await Write(new SetGoal(label, numerator, unit), cancellationToken);
        var chore = household.Chores[label];
        return new PartialResult(ViewHelpers.ChoreCard(label, chore, oob: true));
    }

    [HttpPost("add")]
    public async Task<IResult> Add([FromForm] string label, CancellationToken cancellationToken)
    {
        var household = await Write(new AddChore(label), cancellationToken);
        var chore = household.Chores[label];
        return new PartialResult(ViewHelpers.ChoreCard(label, chore));
    }

    [HttpPost("rename")]
    public async Task<IResult> Rename(
        [FromForm] string oldLabel,
        [FromForm] string newLabel,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(newLabel) || newLabel == oldLabel)
        {
            var current = await Get(cancellationToken);
            var chore = current.Chores.GetValueOrDefault(oldLabel);
            if (chore is null)
                return Results.NotFound();

            // TODO: should get new old id!
            return new PartialResult(ViewHelpers.ChoreCard(oldLabel, chore, oob: true));
        }

        var household = await Write(new RenameChore(oldLabel, newLabel), cancellationToken);
        var renamed = household.Chores[newLabel];
        return new ModalResult(ViewHelpers.EditChore(newLabel, renamed));
    }

    [HttpPost("remove")]
    public async Task<IResult> Remove([FromForm] string label, CancellationToken cancellationToken)
    {
        var household = await Write(new RemoveChore(label), cancellationToken);
        return new PartialResult(
            // language=html
            $"""<div hx-swap-oob='delete' id='{ViewHelpers.ChoreHtmlId(label)}'></div>"""
        );
    }

    [HttpPost("do")]
    public async Task<IResult> Do([FromForm] string label, CancellationToken cancellationToken)
    {
        var household = await Write(new DoChore(label), cancellationToken);
        var chore = household.Chores[label];
        return new PartialResult(ViewHelpers.ChoreCard(label, chore, oob: true));
    }

    [HttpPost("undo")]
    public async Task<IResult> Undo(
        [FromForm] string label,
        [FromForm] DateTimeOffset timestamp,
        CancellationToken cancellationToken
    )
    {
        var household = await Write(new UndoChore(label, timestamp), cancellationToken);
        var chore = household.Chores[label];
        return new PageResult(
            ViewHelpers.ChoreCard(label, chore, oob: true)
        );
    }
    // TODO: swap is janky..
}
