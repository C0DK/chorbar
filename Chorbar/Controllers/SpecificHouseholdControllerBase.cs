using Chorbar.Model;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

public abstract class SpecificHouseholdControllerBase(IHouseholdStore store) : Controller
{
    private readonly IHouseholdStore _store = store;

    [FromRoute]
    public HouseholdId HouseholdId { get; set; }

    protected ValueTask<Household> Get(CancellationToken cancellationToken) =>
        _store.Read(HouseholdId, cancellationToken);

    protected ValueTask<Household> Write(
        HouseholdEventPayload payload,
        CancellationToken cancellationToken
    ) => _store.Write(HouseholdId, payload, cancellationToken);

    protected ValueTask<Household> Write(
        IEnumerable<HouseholdEventPayload> payloads,
        CancellationToken cancellationToken
    ) => _store.Write(HouseholdId, payloads, cancellationToken);
}
