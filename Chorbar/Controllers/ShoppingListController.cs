using System.Collections.Immutable;
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
    public async Task<IResult> Add(
        [FromForm] string label,
        [FromForm] string? category,
        CancellationToken cancellationToken
    )
    {
        var household = await Write(new AddToShoppingList(label, category), cancellationToken);

        var newItem = household.ShoppingListItems.Last();
        return new PartialResult(Render(newItem));
    }

    [HttpPost("add-category")]
    public async Task<IResult> AddCategory(
        [FromForm] string label,
        CancellationToken cancellationToken
    ) => Render(await Write(new AddShoppingListCategory(label), cancellationToken));

    [HttpPost("set-category")]
    public async Task<IResult> SetCategory(
        [FromForm] int[] itemId,
        [FromForm] string? category,
        CancellationToken cancellationToken
    ) =>
        Render(
            await Write(
                new SetShoppingListCategoryItems(
                    category?.Trim()?.Equals("misc", StringComparison.InvariantCultureIgnoreCase)
                        is true
                        ? null
                        : category,
                    itemId.ToImmutableArray()
                ),
                cancellationToken
            )
        );

    [HttpGet("edit-category")]
    public async Task<IResult> EditCategory(string label, CancellationToken cancellationToken) =>
        new ModalResult(new EditShoppingCategory(label: label));

    [HttpPost("rename-category")]
    public async Task<IResult> RenameCategory(
        [FromForm] string label,
        [FromForm] string newLabel,
        CancellationToken cancellationToken
    ) => Render(await Write(new RenameShoppingListCategory(label, newLabel), cancellationToken));

    [HttpPost("delete-category")]
    public async Task<IResult> DeleteCategory(
        [FromForm] string label,
        CancellationToken cancellationToken
    ) => Render(await Write(new DeleteShoppingListCategory(label), cancellationToken));

    [HttpPost("sort-categories")]
    public async Task<IResult> SortCategories(
        [FromForm] string?[] category,
        CancellationToken cancellationToken
    ) =>
        Render(
            await Write(
                new SortCategories(
                    category
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Cast<string>()
                        .ToImmutableArray()
                ),
                cancellationToken
            )
        );

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

    private IResult Render(Household household)
    {
        var miscItems = household.ShoppingListItems.Where(item => item.Category is null);

        return new PartialResult(
            new ShoppingList(
                categories: household
                    .ShoppingListCategories.Append(null)
                    .Select(category =>
                    {
                        var items = household
                            .ShoppingListItems.Where(item => item.Category == category)
                            .OrderBy(item => item.Order);

                        return new Chorbar.Templates.ShoppingListCategoryList(
                            label: category,
                            hasLabel: category is not null,
                            openItems: items.Where(item => !item.IsChecked).Select(Render),
                            checkedItems: items
                                .Where(item => item.CheckedOffRecently(TimeProvider.System))
                                .Select(Render)
                        );
                    })
            ),
            closeModal: true // ??
        );
    }

    private Chorbar.Templates.ShoppingListItem Render(Chorbar.Model.ShoppingListItem item) =>
        new Chorbar.Templates.ShoppingListItem(
            isChecked: item.IsChecked,
            id: item.Id,
            Label: item.Label
        );
}
