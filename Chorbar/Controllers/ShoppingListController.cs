using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

[Authorize]
[Route("household/{householdId:int}/shopping_list/")]
public class ShoppingListController(HouseholdStore store) : SpecificHouseholdControllerBase(store)
{
    [HttpGet("")]
    public async Task<IResult> List(CancellationToken cancellationToken) =>
        Render(await Get(cancellationToken));

    [HttpPost("add")]
    public async Task<IResult> Add([FromForm] string label, CancellationToken cancellationToken) =>
        Render(await Write(new AddToShoppingList(label), cancellationToken));

    [HttpPost("{itemId:int}/checked")]
    public async Task<IResult> Check(int itemId, CancellationToken cancellationToken) =>
        Render(
            await Write(
                Request.Form.GetCheckbox("isChecked")
                    ? new CheckOffShoppingListItem(itemId)
                    : new UnCheckOffShoppingListItem(itemId),
                cancellationToken
            )
        );

    [HttpPost("{itemId:int}/rename")]
    public async Task<IResult> Rename(
        [FromForm] int itemId,
        [FromForm] string newLabel,
        CancellationToken cancellationToken
    ) => Render(await Write(new RenameShoppingListItem(itemId, newLabel), cancellationToken));

    private IResult Render(Household household) =>
        new PartialResult(
            new ShoppingList(
                currentItems: household.ShoppingList.Select(Render),
                checkedItems: household.RecentlyCheckedItems().Select(Render)
            )
        );

    private Chorbar.Templates.ShoppingListItem Render(Chorbar.Model.ShoppingListItem item) =>
        new Chorbar.Templates.ShoppingListItem(
            isChecked: item.IsChecked,
            order: 1,
            id: item.Id,
            Label: item.Label
        );
}
