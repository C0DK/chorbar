using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record EnableTodoList(bool enabled) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "enable_todo_list";

    public override string EventKind => Kind;

    public override bool IsValid(Household household, DateTimeOffset now) => true;

    public override Household Apply(Household household, Email actor, DateTimeOffset timestamp) =>
        household with
        {
            TodoListEnabled = enabled,
        };
}
