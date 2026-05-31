using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record CheckTodo(int ItemId, bool State) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "check_todo";

    public override string EventKind => Kind;

    public override bool IsValid(Household household, DateTimeOffset now) =>
        household.TodoListEnabled && household.Todos.Any(todo => todo.Id == ItemId);

    public override Household Apply(Household household, Email actor, DateTimeOffset timestamp) =>
        household with
        {
            Todos = household
                .Todos.Select(todo =>
                    todo.Id == ItemId ? todo with { Done = State ? timestamp : null } : todo
                )
                .ToImmutableArray(),
        };
}
