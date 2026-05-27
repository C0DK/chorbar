using System.Collections.Immutable;
using Chorbar.Model;
using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

[Authorize]
[Route("household/{householdId:int}/meal_plan/")]
public class MealPlanController(HouseholdStore store) : SpecificHouseholdControllerBase(store)
{
    private static readonly string[] Weekdays =
    [
        "Sunday",
        "Monday",
        "Tuesday",
        "Wednesday",
        "Thursday",
        "Friday",
        "Saturday",
    ];

    [HttpGet("")]
    public async Task<IResult> List(CancellationToken cancellationToken) =>
        Render(await Get(cancellationToken));

    [HttpPost("add")]
    public async Task<IResult> Add([FromForm] string label, CancellationToken cancellationToken)
    {
        var household = await Write(new AddMealPlanEntry(label), cancellationToken);
        var newItem = household.MealPlan.Last();
        return new PartialResult(RenderItem(newItem, 0));
    }

    [HttpGet("{itemId:int}")]
    public async Task<IResult> GetItem(int itemId, CancellationToken cancellationToken)
    {
        var household = await Get(cancellationToken);
        var sorted = household.MealPlan.OrderBy(m => m.Order).ToImmutableArray();
        var index = sorted.IndexOf(sorted.FirstOrDefault(m => m.Id == itemId));
        var item = sorted.First(m => m.Id == itemId);
        return new PartialResult(RenderItem(item, index));
    }

    [HttpGet("{itemId:int}/edit")]
    public async Task<IResult> EditItem(int itemId, CancellationToken cancellationToken)
    {
        var household = await Get(cancellationToken);
        var item = household.MealPlan.First(m => m.Id == itemId);
        return new PartialResult(new MealPlanEditItem(id: itemId, Label: item.Label));
    }

    [HttpPost("{itemId:int}/rename")]
    public async Task<IResult> Rename(
        int itemId,
        [FromForm] string newLabel,
        CancellationToken cancellationToken
    ) => Render(await Write(new RenameMealPlanEntry(itemId, newLabel), cancellationToken));

    [HttpPost("{itemId:int}/checked")]
    public async Task<IResult> Check(int itemId, CancellationToken cancellationToken) =>
        Render(
            await Write(
                new CheckMealPlanEntry(itemId, Request.Form.GetCheckbox("isChecked")),
                cancellationToken
            )
        );

    [HttpPost("sort")]
    public async Task<IResult> Sort(
        [FromForm] int[] itemId,
        CancellationToken cancellationToken
    ) => Render(await Write(new OrderMealPlan(itemId.ToImmutableArray()), cancellationToken));

    private IResult Render(Household household)
    {
        var sorted = household.MealPlan.OrderBy(m => m.Order).ToImmutableArray();
        var todayDayOfWeek = (int)TimeProvider.System.GetLocalNow().DayOfWeek;
        var firstCheckedAfterNoon =
            sorted.Length > 0 && sorted[0].CheckedOffTodayAfterNoon(TimeProvider.System);

        return new PartialResult(
            new Chorbar.Templates.MealPlan(
                items: sorted.Select(
                    (item, index) => RenderItem(item, index, todayDayOfWeek, firstCheckedAfterNoon)
                )
            )
        );
    }

    private Chorbar.Templates.MealPlanItem RenderItem(
        Chorbar.Model.MealPlanItem item,
        int index,
        int? todayDayOfWeek = null,
        bool firstCheckedAfterNoon = false
    )
    {
        var today = todayDayOfWeek ?? (int)TimeProvider.System.GetLocalNow().DayOfWeek;
        var weekday = Weekdays[(today + index) % 7];
        var isToday = index == 0 && firstCheckedAfterNoon;
        return new Chorbar.Templates.MealPlanItem(
            weekday: weekday,
            isChecked: item.IsDone,
            isTodayDone: isToday,
            id: item.Id,
            Label: item.Label
        );
    }
}
