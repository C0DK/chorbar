using System.Collections.Immutable;
using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

[Route("household/{householdId:int}/todo/")]
public class TodoController(IHouseholdStore store) : SpecificHouseholdControllerBase(store)
{
    [HttpGet("")]
    public async Task<IResult> List(CancellationToken cancellationToken) =>
        Render(await Get(cancellationToken));

    [HttpPost("add")]
    public async Task<IResult> Add(
        [FromForm] string label,
        [FromForm] string? category,
        CancellationToken cancellationToken
    )
    {
        var household = await Write(new AddTodo(label), cancellationToken);

        var newItem = household.Todos.Last();
        return new PartialResult(Render(newItem));
    }

    [HttpGet("{itemId:int}")]
    public async Task<IResult> GetItem(int itemId, CancellationToken cancellationToken)
    {
        var household = await Get(cancellationToken);
        var item = household.Todos.First(i => i.Id == itemId);
        return new PartialResult(Render(item));
    }

    [HttpGet("{itemId:int}/edit")]
    public async Task<IResult> EditItem(int itemId, CancellationToken cancellationToken)
    {
        var household = await Get(cancellationToken);
        var item = household.Todos.First(i => i.Id == itemId);
        return new PartialResult(new TodoListEditItem(id: itemId, Label: item.Label));
    }

    [HttpPost("{itemId:int}/rename")]
    public async Task<IResult> Rename(
        int itemId,
        [FromForm] string newLabel,
        CancellationToken cancellationToken
    ) => Render(await Write(new RenameTodo(itemId, newLabel), cancellationToken));

    [HttpPost("{itemId:int}/checked")]
    public async Task<IResult> Check(int itemId, CancellationToken cancellationToken) =>
        Render(
            await Write(
                new CheckTodo(itemId, Request.Form.GetCheckbox("isChecked")),
                cancellationToken
            )
        );

    [HttpPost("sort")]
    public async Task<IResult> Sort([FromForm] int[] itemId, CancellationToken cancellationToken) =>
        Render(await Write(new OrderTodo(itemId.ToImmutableArray()), cancellationToken));

    private IResult Render(Household household)
    {
        return new PartialResult(
            new TodoList(
                openItems: household
                    .Todos.Where(item => !item.IsDone)
                    .OrderBy(todo => todo.Order)
                    .Select(Render),
                checkedItems: household
                    .Todos.Where(item => item.CheckedOffRecently(TimeProvider.System))
                    .OrderByDescending(todo => todo.Order)
                    .Select(Render)
            ),
            closeModal: true // ??
        );
    }

    private Chorbar.Templates.TodoListItem Render(Chorbar.Model.TodoItem item) =>
        new Chorbar.Templates.TodoListItem(isChecked: item.IsDone, id: item.Id, Label: item.Label);
}
