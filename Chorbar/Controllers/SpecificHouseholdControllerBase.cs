using Chorbar.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

[Authorize]
public abstract class SpecificHouseholdControllerBase(HouseholdStore store) : Controller
{
    private readonly HouseholdStore _store = store;

    [FromRoute]
    public HouseholdId HouseholdId { get; set; }

    protected ValueTask<Household> Get(CancellationToken cancellationToken) =>
        _store.Read(HouseholdId, cancellationToken);

    protected ValueTask<Household> Write(
        HouseholdEventPayload payload,
        CancellationToken cancellationToken
    ) => _store.Write(HouseholdId, payload, cancellationToken);

}
