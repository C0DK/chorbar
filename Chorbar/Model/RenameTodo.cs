using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record RenameTodo(int Id, string NewLabel) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "rename_todo";

    public override string EventKind => Kind;

    public override bool IsValid(Household household, DateTimeOffset now) =>
        household.TodoListEnabled && household.Todos.Any(todo => todo.Id == Id);

    public override Household Apply(Household household, Email actor, DateTimeOffset timestamp) =>
        household with
        {
            Todos = household
                .Todos.Select(todo => todo.Id == Id ? todo with { Label = NewLabel } : todo)
                .ToImmutableArray(),
        };
}
