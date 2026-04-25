namespace Chorbar.Model;

public record HouseholdEvent(
    HouseholdId HouseholdId,
    int Version,
    DateTimeOffset Timestamp,
    HouseholdEventPayload Payload,
    Email CreatedBy
)
{
    public Household Apply(Household household) =>
        Payload.Apply(household, Timestamp) with
        {
            History = household.History.Add(this),
        };

    public override string ToString() =>
        $"{Payload}[{Version}|{Timestamp:s}|{CreatedBy}@{HouseholdId}]";
}
