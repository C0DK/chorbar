using System.Collections.Immutable;
using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

[Route("household/demo/todo/")]
public class DemoTodoController(DemoHouseholdStore store) : Controller
{
    [HttpGet("")]
    public IResult List() => Render(store.Read());

    [HttpPost("add")]
    public IResult Add([FromForm] string label)
    {
        var household = store.Write(new AddTodo(label));
        var newItem = household.Todos.Last();
        return new PartialResult(Render(newItem));
    }

    [HttpGet("{itemId:int}")]
    public IResult GetItem(int itemId)
    {
        var household = store.Read();
        var item = household.Todos.First(i => i.Id == itemId);
        return new PartialResult(Render(item));
    }

    [HttpGet("{itemId:int}/edit")]
    public IResult EditItem(int itemId)
    {
        var household = store.Read();
        var item = household.Todos.First(i => i.Id == itemId);
        return new PartialResult(new TodoListEditItem(id: itemId, Label: item.Label));
    }

    [HttpPost("{itemId:int}/rename")]
    public IResult Rename(int itemId, [FromForm] string newLabel) =>
        Render(store.Write(new RenameTodo(itemId, newLabel)));

    [HttpPost("{itemId:int}/checked")]
    public IResult Check(int itemId) =>
        Render(store.Write(new CheckTodo(itemId, Request.Form.GetCheckbox("isChecked"))));

    [HttpPost("sort")]
    public IResult Sort([FromForm] int[] itemId) =>
        Render(store.Write(new OrderTodo(itemId.ToImmutableArray())));

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
            closeModal: true
        );
    }

    private static TodoListItem Render(TodoItem item) =>
        new(isChecked: item.IsDone, id: item.Id, Label: item.Label);
}
