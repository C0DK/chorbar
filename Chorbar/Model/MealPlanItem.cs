namespace Chorbar.Model;

public record MealPlanItem(int Id, string Label, DateTimeOffset? Done, int Order = -1)
{
    public bool IsDone => Done is not null;

    public bool CheckedOffTodayAfterNoon(TimeProvider timeProvider)
    {
        if (Done is null)
            return false;
        var localNow = timeProvider.GetLocalNow();
        var doneLocal = Done.Value.ToLocalTime();
        return doneLocal.Date == localNow.Date && doneLocal.Hour >= 12;
    }
}
