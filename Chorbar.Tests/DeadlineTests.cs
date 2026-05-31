using Chorbar.Controllers;
using Chorbar.Model;
using Microsoft.Extensions.Time.Testing;

namespace Chorbar.Tests;

public class DeadlineTests
{
    private static DateOnly Today => DateTimeOffset.Now.GetCalendarDate();

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

        Assert.That(subject.Deadline(), Is.EqualTo(new DateOnly(2026, 05, 25)));
    }

    [Test]
    public void Overdue() =>
        Assert.That(ViewHelpers.DeadlineText(Today.AddDays(-1)), Is.EqualTo("Overdue"));

    [Test]
    public void OverdueMultipleDays() =>
        Assert.That(ViewHelpers.DeadlineText(Today.AddDays(-5)), Is.EqualTo("5d overdue"));

    [Test]
    public void DueToday() => Assert.That(ViewHelpers.DeadlineText(Today), Is.EqualTo("Today"));

    [Test]
    public void DueTomorrow() =>
        Assert.That(ViewHelpers.DeadlineText(Today.AddDays(1)), Is.EqualTo("Tomorrow"));

    [Test]
    public void DueWithinSixDays_ShowsWeekday()
    {
        var fourDaysFromNow = DateTimeOffset.Now.Date.AddDays(4);
        var deadline = new DateTimeOffset(fourDaysFromNow, DateTimeOffset.Now.Offset);
        var provider = new FakeTimeProvider(
            new DateTimeOffset(2026, 05, 31, 20, 00, 00, TimeSpan.Zero)
        );

        Assert.That(
            ViewHelpers.DeadlineText(new DateOnly(2026, 06, 03), provider),
            Is.EqualTo("Wednesday")
        );
    }

    [Test]
    public void DueWithinSixDays_ShowsWeekday2()
    {
        var fourDaysFromNow = DateTimeOffset.Now.Date.AddDays(6);
        var deadline = new DateTimeOffset(fourDaysFromNow, DateTimeOffset.Now.Offset);
        var provider = new FakeTimeProvider(
            new DateTimeOffset(2026, 05, 31, 20, 00, 00, TimeSpan.Zero)
        );

        Assert.That(
            ViewHelpers.DeadlineText(new DateOnly(2026, 06, 06), provider),
            Is.EqualTo("Saturday")
        );
    }

    [Test]
    public void DueInMoreThanSixDays_ShowsTimeUntil()
    {
        var fourDaysFromNow = DateTimeOffset.Now.Date.AddDays(6);
        var deadline = new DateTimeOffset(fourDaysFromNow, DateTimeOffset.Now.Offset);
        var provider = new FakeTimeProvider(
            new DateTimeOffset(2026, 05, 31, 20, 00, 00, TimeSpan.Zero)
        );

        Assert.That(
            ViewHelpers.DeadlineText(new DateOnly(2026, 06, 10), provider),
            Is.EqualTo("In 10 days")
        );
    }

    [Test]
    public void DueIn6Months()
    {
        var fourDaysFromNow = DateTimeOffset.Now.Date.AddDays(6);
        var deadline = new DateTimeOffset(fourDaysFromNow, DateTimeOffset.Now.Offset);
        var provider = new FakeTimeProvider(
            new DateTimeOffset(2026, 05, 31, 20, 00, 00, TimeSpan.Zero)
        );

        Assert.That(
            ViewHelpers.DeadlineText(new DateOnly(2026, 12, 24), provider),
            Is.EqualTo("24 December")
        );
    }

    [Test]
    public void DueIn11Months()
    {
        var fourDaysFromNow = DateTimeOffset.Now.Date.AddDays(6);
        var deadline = new DateTimeOffset(fourDaysFromNow, DateTimeOffset.Now.Offset);
        var provider = new FakeTimeProvider(
            new DateTimeOffset(2026, 05, 31, 20, 00, 00, TimeSpan.Zero)
        );

        Assert.That(
            ViewHelpers.DeadlineText(new DateOnly(2027, 05, 01), provider),
            Is.EqualTo("1 May 2027")
        );
    }

    [Test]
    public void Null_ReturnsNever()
    {
        Assert.That(ViewHelpers.DeadlineText(null), Is.EqualTo("never"));
    }
}
