using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record CreateNewHousehold(string Name) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "create_household";

    public override string EventKind => Kind;

    //public Household Create(HouseholdId id, Email Creator) => new Household(Id: id, Name: Name, Members:[Creator], Chores: [], History: [ this]])

    public override bool IsValid(Household household, DateTimeOffset now) =>
        throw new InvalidOperationException();

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        throw new InvalidOperationException();
}
