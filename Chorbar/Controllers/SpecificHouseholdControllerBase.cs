using Chorbar.Model;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

public abstract class SpecificHouseholdControllerBase(IHouseholdStore store) : Controller
{
    protected IHouseholdStore Store { get; } = store;

    [FromRoute]
    public HouseholdId HouseholdId { get; set; }

    protected ValueTask<Household> Get(CancellationToken cancellationToken) =>
        Store.Read(HouseholdId, cancellationToken);

    protected ValueTask<Household> Write(
        HouseholdEventPayload payload,
        CancellationToken cancellationToken
    ) => Store.Write(HouseholdId, payload, cancellationToken);

    protected ValueTask<Household> Write(
        IEnumerable<HouseholdEventPayload> payloads,
        CancellationToken cancellationToken
    ) => Store.Write(HouseholdId, payloads, cancellationToken);
}
