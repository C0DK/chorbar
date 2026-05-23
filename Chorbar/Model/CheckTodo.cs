using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record CheckTodo(int Id, bool State) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "check_todo";

    public override string EventKind => Kind;

    public override bool IsValid(Household household, DateTimeOffset now) =>
        household.TodoListEnabled && household.Todos.Any(todo => todo.Id == Id);

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            Todos = household
                .Todos.Select(todo =>
                    todo.Id == Id ? todo with { Done = State ? timestamp : null } : todo
                )
                .ToImmutableArray(),
        };
}
