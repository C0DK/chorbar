using System.Collections.Immutable;
using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

[Route("household/demo/shopping_list/")]
public class DemoShoppingListController(DemoHouseholdStore store) : Controller
{
    [HttpGet("")]
    public IResult List() => Render(store.Read());

    [HttpPost("add")]
    public IResult Add([FromForm] string label, [FromForm] string? category)
    {
        var household = store.Write(new AddToShoppingList(label, category));
        var newItem = household.ShoppingListItems.Last();
        return new PartialResult(Render(newItem));
    }

    [HttpPost("add-category")]
    public IResult AddCategory([FromForm] string label) =>
        Render(store.Write(new AddShoppingListCategory(label)));

    [HttpPost("set-category")]
    public IResult SetCategory([FromForm] int[] itemId, [FromForm] string? category) =>
        Render(
            store.Write(
                new SetShoppingListCategoryItems(
                    category?.Trim()?.Equals("misc", StringComparison.InvariantCultureIgnoreCase)
                        is true
                        ? null
                        : category,
                    itemId.ToImmutableArray()
                )
            )
        );

    [HttpGet("edit-category")]
    public IResult EditCategory(string label) =>
        new ModalResult(new EditShoppingCategory(label: label));

    [HttpPost("rename-category")]
    public IResult RenameCategory([FromForm] string label, [FromForm] string newLabel) =>
        Render(store.Write(new RenameShoppingListCategory(label, newLabel)));

    [HttpPost("delete-category")]
    public IResult DeleteCategory([FromForm] string label) =>
        Render(store.Write(new DeleteShoppingListCategory(label)));

    [HttpPost("{itemId:int}/checked")]
    public IResult Check(int itemId) =>
        Render(
            store.Write(
                Request.Form.GetCheckbox("isChecked")
                    ? new CheckOffShoppingListItem(itemId)
                    : new UnCheckOffShoppingListItem(itemId)
            )
        );

    [HttpGet("{itemId:int}")]
    public IResult GetItem(int itemId)
    {
        var household = store.Read();
        var item = household.ShoppingListItems.First(i => i.Id == itemId);
        return new PartialResult(Render(item));
    }

    [HttpGet("{itemId:int}/edit")]
    public IResult EditItem(int itemId)
    {
        var household = store.Read();
        var item = household.ShoppingListItems.First(i => i.Id == itemId);
        return new PartialResult(new ShoppingListItemEdit(id: itemId, Label: item.Label));
    }

    [HttpPost("{itemId:int}/rename")]
    public IResult Rename(int itemId, [FromForm] string newLabel) =>
        Render(store.Write(new RenameShoppingListItem(itemId, newLabel)));

    private IResult Render(Household household)
    {
        return new PartialResult(
            new ShoppingList(
                categories: household
                    .ShoppingListCategories.Append(null)
                    .Select(category =>
                    {
                        var items = household
                            .ShoppingListItems.Where(item => item.Category == category)
                            .OrderBy(item => item.Order);

                        return new ShoppingListCategoryList(
                            label: category,
                            hasLabel: category is not null,
                            openItems: items.Where(item => !item.IsChecked).Select(Render),
                            checkedItems: items
                                .Where(item => item.CheckedOffRecently(TimeProvider.System))
                                .Select(Render)
                        );
                    })
            ),
            closeModal: true
        );
    }

    private static Chorbar.Templates.ShoppingListItem Render(Chorbar.Model.ShoppingListItem item) =>
        new(isChecked: item.IsChecked, id: item.Id, Label: item.Label);
}
