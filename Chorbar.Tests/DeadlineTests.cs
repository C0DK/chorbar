using Chorbar.Controllers;
using Chorbar.Model;

namespace Chorbar.Tests;

public class DeadlineTests
{
    [Test]
    public void DeadlineIsEndOfDay()
    {
        var subject = new Chore(
            new DateTimeOffset(2026, 01, 01, 00, 00, 00, TimeSpan.Zero),
            [
                new DateTimeOffset(2026, 05, 19, 19, 00, 00, TimeSpan.Zero),
                new DateTimeOffset(2026, 05, 22, 16, 00, 00, TimeSpan.Zero),
            ],
            new Goal(3, DateUnit.Day)
        );

        Assert.That(
            subject.Deadline(),
            Is.EqualTo(new DateTimeOffset(2026, 05, 26, 00, 00, 00, TimeSpan.Zero))
        );
    }

    [Test]
    public void Overdue()
    {
        var past = DateTimeOffset.Now.AddHours(-1);

        Assert.That(ViewHelpers.DeadlineText(past), Is.EqualTo("Overdue"));
    }

    [Test]
    public void OverdueMultipleDays()
    {
        var past = DateTimeOffset.Now.AddDays(-5);

        Assert.That(ViewHelpers.DeadlineText(past), Is.EqualTo("5d overdue"));
    }

    [Test]
    public void DueToday_BeforeMidnight()
    {
        var endOfToday = DateTimeOffset.Now.Date.AddDays(1).AddMinutes(-1);
        var deadline = new DateTimeOffset(endOfToday, DateTimeOffset.Now.Offset);

        Assert.That(ViewHelpers.DeadlineText(deadline), Is.EqualTo("Today"));
    }

    [Test]
    public void DueTomorrow_JustAfterMidnight()
    {
        var startOfTomorrow = DateTimeOffset.Now.Date.AddDays(1).AddMinutes(1);
        var deadline = new DateTimeOffset(startOfTomorrow, DateTimeOffset.Now.Offset);

        Assert.That(ViewHelpers.DeadlineText(deadline), Is.EqualTo("Tomorrow"));
    }

    [Test]
    public void DueTomorrow_EndOfTomorrow()
    {
        var endOfTomorrow = DateTimeOffset.Now.Date.AddDays(2).AddMinutes(-1);
        var deadline = new DateTimeOffset(endOfTomorrow, DateTimeOffset.Now.Offset);

        Assert.That(ViewHelpers.DeadlineText(deadline), Is.EqualTo("Tomorrow"));
    }

    [Test]
    public void DueWithinSixDays_ShowsWeekday()
    {
        var fourDaysFromNow = DateTimeOffset.Now.Date.AddDays(4);
        var deadline = new DateTimeOffset(fourDaysFromNow, DateTimeOffset.Now.Offset);

        Assert.That(ViewHelpers.DeadlineText(deadline), Is.EqualTo(deadline.ToString("dddd")));
    }

    [Test]
    public void DueInMoreThanSixDays_ShowsTimeUntil()
    {
        var future = DateTimeOffset.Now.AddDays(10);

        Assert.That(ViewHelpers.DeadlineText(future), Does.StartWith("in "));
    }

    [Test]
    public void Null_ReturnsNever()
    {
        Assert.That(ViewHelpers.DeadlineText(null), Is.EqualTo("never"));
    }
}
