using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record DeleteHousehold : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "delete_household";

    public override string EventKind => Kind;

    public override bool IsValid(Household household) => !household.IsDeleted;

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            IsDeleted = true,
        };
}
