using Chorbar.Model;

namespace Chorbar;

public interface IHouseholdStore
{
    ValueTask<HouseholdId> Create(string name, CancellationToken cancellationToken);
    ValueTask Delete(HouseholdId id, CancellationToken cancellationToken);
    ValueTask<Household> Write(
        HouseholdId id,
        HouseholdEventPayload payload,
        CancellationToken cancellationToken
    );
    ValueTask<Household> Write(
        HouseholdId id,
        IEnumerable<HouseholdEventPayload> payloads,
        CancellationToken cancellationToken
    );
    IAsyncEnumerable<Household> List(CancellationToken cancellationToken);
    ValueTask<Household> Read(HouseholdId id, CancellationToken cancellationToken);
}
