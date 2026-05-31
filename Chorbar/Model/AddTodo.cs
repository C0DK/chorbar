using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record AddTodo(string Label) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "add_todo";

    public override string EventKind => Kind;

    public override bool IsValid(Household household, DateTimeOffset now) =>
        household.TodoListEnabled;

    public override Household Apply(Household household, Email actor, DateTimeOffset timestamp)
    {
        var id = household.Todos.Length > 0 ? (household.Todos.Max(todo => todo.Id) + 1) : 1;
        return household with { Todos = household.Todos.Add(new TodoItem(id, Label, null, id)) };
    }
}
