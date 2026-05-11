using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record RemoveMember(Email User) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "remove_member";

    public override string EventKind => Kind;

    public override bool IsValid(Household household, DateTimeOffset now) =>
        household.Members.Contains(User) && household.Creator != User;

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            Members = household.Members.Remove(User),
        };
}
