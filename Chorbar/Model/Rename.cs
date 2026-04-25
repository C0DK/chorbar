using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record Rename(string NewName) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "rename";

    public override string EventKind => Kind;

    public override bool IsValid(Household household) => !string.IsNullOrWhiteSpace(NewName);

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            Name = NewName.Trim(),
        };
}
