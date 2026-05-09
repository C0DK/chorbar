using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record GenerateIcalToken(string Token) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "generate_ical_token";

    public override string EventKind => Kind;

    public override bool IsValid(Household household) => true;

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with { IcalToken = Token };
}
