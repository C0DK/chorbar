using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

[Route("household/{householdId:int}/chore/")]
public class ChoresController(IHouseholdStore store) : SpecificHouseholdControllerBase(store)
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
        return new PartialResult(ViewHelpers.ChoreCard(label, chore));
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
        if (string.IsNullOrWhiteSpace(newLabel) || newLabel.Trim() == oldLabel.Trim())
        {
            var current = await Get(cancellationToken);
            var chore = current.Chores.GetValueOrDefault(oldLabel);
            if (chore is null)
                return Results.NotFound();

            // TODO: should get new old id!
            return new PartialResult(
                new EditChoreRenameForm(label: newLabel)
                    + ViewHelpers.ChoreCard(newLabel, chore, oob: true)
            );
        }

        var household = await Write(new RenameChore(oldLabel, newLabel), cancellationToken);
        var renamed = household.Chores[newLabel];
        return new PartialResult(
            new EditChoreRenameForm(label: newLabel)
                + ViewHelpers.ChoreCard(
                    newLabel,
                    renamed,
                    oob: true,
                    oobSwapId: ViewHelpers.ChoreHtmlId(oldLabel)
                )
        );
    }

    [HttpPost("remove")]
    public async Task<IResult> Remove([FromForm] string label, CancellationToken cancellationToken)
    {
        var household = await Write(new RemoveChore(label), cancellationToken);
        return new PartialResult("", closeModal: true);
    }

    [HttpPost("do")]
    public async Task<IResult> Do([FromForm] string label, CancellationToken cancellationToken)
    {
        var household = await Write(new DoChore(label), cancellationToken);
        var chore = household.Chores[label];
        return new PartialResult(ViewHelpers.ChoreCard(label, chore));
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

        return new ModalResult(
            ViewHelpers.EditChore(label, chore),
            ViewHelpers.ChoreCard(label, chore, oob: true)
        );
    }

    [HttpPost("do_past")]
    public async Task<IResult> DoPast(
        [FromForm] string label,
        [FromForm] DateOnly when,
        CancellationToken cancellationToken
    )
    {
        var household = await Write(new AddPastChoreCompletion(label, when), cancellationToken);
        var chore = household.Chores[label];
        return new ModalResult(
            ViewHelpers.EditChore(label, chore),
            ViewHelpers.ChoreCard(label, chore, oob: true)
        );
    }

    // TODO: swap is janky..
}
