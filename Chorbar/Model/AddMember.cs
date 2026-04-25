using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record AddMember(Email User) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "add_member";

    public override string EventKind => Kind;

    public override bool IsValid(Household household) => true;

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            Members = household.Members.Add(User),
        };
}
