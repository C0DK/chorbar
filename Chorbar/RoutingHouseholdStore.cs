using System.Runtime.CompilerServices;
using Chorbar.Model;

namespace Chorbar;

public class RoutingHouseholdStore(HouseholdStore dbStore, DemoHouseholdStore demoStore)
    : IHouseholdStore
{
    private IHouseholdStore Resolve(HouseholdId id) =>
        id == DemoHouseholdStore.DemoHouseholdId ? demoStore : dbStore;

    public ValueTask<Household> Read(HouseholdId id, CancellationToken cancellationToken) =>
        Resolve(id).Read(id, cancellationToken);

    public ValueTask<Household> Write(
        HouseholdId id,
        HouseholdEventPayload payload,
        CancellationToken cancellationToken
    ) => Resolve(id).Write(id, payload, cancellationToken);

    public ValueTask<Household> Write(
        HouseholdId id,
        IEnumerable<HouseholdEventPayload> payloads,
        CancellationToken cancellationToken
    ) => Resolve(id).Write(id, payloads, cancellationToken);

    public ValueTask Delete(HouseholdId id, CancellationToken cancellationToken) =>
        Resolve(id).Delete(id, cancellationToken);

    public IAsyncEnumerable<Household> List(CancellationToken cancellationToken) =>
        dbStore.List(cancellationToken);

    public ValueTask<HouseholdId> Create(string name, CancellationToken cancellationToken) =>
        dbStore.Create(name, cancellationToken);
}
