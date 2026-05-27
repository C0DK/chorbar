using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

[Route("household/demo/")]
public class DemoController(DemoHouseholdStore store) : Controller
{
    [HttpGet("")]
    public IResult Index()
    {
        var household = store.Read();
        HttpContext.UpdateHousehold(household);
        return new PageResult(
            new HouseholdPage(
                shoppingListEnabled: false,
                todoListEnabled: false,
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

    [HttpGet("chore/")]
    public IResult Card([FromQuery] string label)
    {
        var household = store.Read();
        var chore = household.Chores.GetValueOrDefault(label);
        if (chore is null)
            return Results.NotFound();

        return new PartialResult(ViewHelpers.ChoreCard(label, chore));
    }

    [HttpGet("chore/edit")]
    public IResult Details([FromQuery] string label)
    {
        var household = store.Read();
        var chore = household.Chores.GetValueOrDefault(label);
        if (chore is null)
            return Results.NotFound();

        return new ModalResult(ViewHelpers.EditChore(label, chore));
    }

    [HttpPost("chore/goal")]
    public IResult Goal([FromForm] string label, [FromForm] DateUnit unit, [FromForm] int numerator)
    {
        var household = store.Write(new SetGoal(label, numerator, unit));
        var chore = household.Chores[label];
        return new PartialResult(ViewHelpers.ChoreCard(label, chore));
    }

    [HttpPost("chore/add")]
    public IResult Add([FromForm] string label)
    {
        var household = store.Write(new AddChore(label));
        var chore = household.Chores[label];
        return new PartialResult(ViewHelpers.ChoreCard(label, chore));
    }

    [HttpPost("chore/rename")]
    public IResult Rename([FromForm] string oldLabel, [FromForm] string newLabel)
    {
        if (string.IsNullOrWhiteSpace(newLabel) || newLabel == oldLabel)
        {
            var current = store.Read();
            var chore = current.Chores.GetValueOrDefault(oldLabel);
            if (chore is null)
                return Results.NotFound();

            return new PartialResult(ViewHelpers.ChoreCard(oldLabel, chore));
        }

        var household = store.Write(new RenameChore(oldLabel, newLabel));
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

    [HttpPost("chore/remove")]
    public IResult Remove([FromForm] string label)
    {
        store.Write(new RemoveChore(label));
        return new PartialResult("", closeModal: true);
    }

    [HttpPost("chore/do")]
    public IResult Do([FromForm] string label)
    {
        var household = store.Write(new DoChore(label));
        var chore = household.Chores[label];
        return new PartialResult(ViewHelpers.ChoreCard(label, chore));
    }

    [HttpPost("chore/undo")]
    public IResult Undo([FromForm] string label, [FromForm] DateTimeOffset timestamp)
    {
        var household = store.Write(new UndoChore(label, timestamp));
        var chore = household.Chores[label];
        return new PageResult(ViewHelpers.ChoreCard(label, chore));
    }

    [HttpPost("chore/do_past")]
    public IResult DoPast([FromForm] string label, [FromForm] DateOnly when)
    {
        var household = store.Write(new AddPastChoreCompletion(label, when));
        var chore = household.Chores[label];
        return new PartialResult(ViewHelpers.EditChore(label, chore));
    }
}
