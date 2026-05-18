using Chorbar.Controllers;
using Chorbar.Model;

namespace Chorbar.Unit;

public class ChoreTest
{
    [Test]
    public void Frequency_Simple()
    {
        var subject = new Chore(t(0), [t(1), t(2)]);

        Assert.That(subject.Frequency(), Is.EqualTo(TimeSpan.FromMinutes(1)));
    }

    [Test]
    public void Frequency_Irregular()
    {
        var subject = new Chore(t(0), [t(5), t(15), t(25)]);

        Assert.That(subject.Frequency(), Is.EqualTo(TimeSpan.FromMinutes(10)));
    }

    [Test]
    public void frequency_Empty()
    {
        var subject = new Chore(t(0), []);

        Assert.That(subject.Frequency(), Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void WorstFrequency_Simple()
    {
        var subject = new Chore(t(0), [t(1), t(2)]);

        Assert.That(subject.WorstFrequency(), Is.EqualTo(TimeSpan.FromMinutes(1)));
    }

    [Test]
    public void WorstFrequency_Irregular()
    {
        var subject = new Chore(t(0), [t(2), t(15), t(17)]);

        Assert.That(subject.WorstFrequency(), Is.EqualTo(TimeSpan.FromMinutes(13)));
    }

    [Test]
    public void Worstfrequency_Empty()
    {
        var subject = new Chore(t(0), []);

        Assert.That(subject.WorstFrequency(), Is.EqualTo(TimeSpan.Zero));
    }

    // Streak tests — use day-based time steps

    [Test]
    public void Streak_EmptyHistory_ReturnsZero()
    {
        var subject = new Chore(d(0), []);

        Assert.That(subject.Streak(d(10)), Is.EqualTo(0));
    }

    [Test]
    public void Streak_SingleCompletion_WithinWindow_ReturnsOne()
    {
        // created day 0, done day 7, frequency = 7 days, allowedLatency = 0.7 days, maxGap = 7.7 days
        // now = day 14 → timeSinceLast = 7 days ≤ 7.7 days → still active
        var subject = new Chore(d(0), [d(7)]);

        Assert.That(subject.Streak(d(14)), Is.EqualTo(1));
    }

    [Test]
    public void Streak_SingleCompletion_TooOld_ReturnsZero()
    {
        // created day 0, done day 7, frequency = 7 days, maxGap = 7.7 days
        // now = day 16 → timeSinceLast = 9 days > 7.7 days → streak broken
        var subject = new Chore(d(0), [d(7)]);

        Assert.That(subject.Streak(d(16)), Is.EqualTo(0));
    }

    [Test]
    public void Streak_MultipleConsecutive_ReturnsCount()
    {
        // done every 7 days, frequency = 7 days, maxGap = 7.7 days
        // now just after last completion → all 3 count
        var subject = new Chore(d(0), [d(7), d(14), d(21)]);

        Assert.That(subject.Streak(d(21)), Is.EqualTo(3));
    }

    [Test]
    public void Streak_BrokenInMiddle_CountsFromLastContinuous()
    {
        // day 0 created, done at 7, 14, then gap of 30 days (missing), then 44, 51
        // frequency ≈ 7 days (median of: 7,7,16,7,7 sorted: 7,7,7,7,16 → median=7)
        // maxGap = 7 + 0.7 = 7.7 days
        // gap between d(14) and d(44) = 30 days > 7.7 → streak broken there
        // streak from d(44) to d(51) = 2
        var subject = new Chore(d(0), [d(7), d(14), d(44), d(51)]);

        Assert.That(subject.Streak(d(51)), Is.EqualTo(2));
    }

    [Test]
    public void Streak_ShortFrequency_OneDayLatency()
    {
        // frequency = 5 days, allowedLatency = max(1, 5/10) = 1 day, maxGap = 6 days
        // done at 0, 5, 10, 16 (6 days gap = exactly at limit)
        var subject = new Chore(d(-5), [d(0), d(5), d(10), d(16)]);

        // gap between d(10) and d(16) = 6 days = maxGap → still counts
        Assert.That(subject.Streak(d(16)), Is.EqualTo(4));
    }

    [Test]
    public void Streak_ShortFrequency_BreaksWhenTooLate()
    {
        // frequency = 5 days, maxGap = 6 days
        // done at 0, 5, 10, 17 (7 days gap > maxGap) → streak resets
        var subject = new Chore(d(-5), [d(0), d(5), d(10), d(17)]);

        Assert.That(subject.Streak(d(17)), Is.EqualTo(1));
    }

    [Test]
    public void Streak_HundredDayFrequency_TenDayLatency()
    {
        // frequency ≈ 100 days, allowedLatency = max(1, 100/10) = 10 days, maxGap = 110 days
        // created day 0, done day 100, 200, 300 → all gaps = 100 days ≤ 110 days
        // now = day 300, timeSinceLast = 0 → active
        var subject = new Chore(d(0), [d(100), d(200), d(300)]);

        Assert.That(subject.Streak(d(300)), Is.EqualTo(3));
    }

    [Test]
    public void Streak_HundredDayFrequency_BreaksAfterElevenDaysLate()
    {
        // frequency ≈ 100 days, maxGap = 110 days
        // done at 100, 200, 311 (111 days gap > 110) → streak resets
        var subject = new Chore(d(0), [d(100), d(200), d(311)]);

        Assert.That(subject.Streak(d(311)), Is.EqualTo(1));
    }

    // DeadlineText tests

    [Test]
    public void DeadlineText_Overdue()
    {
        var past = DateTimeOffset.Now.AddHours(-1);

        Assert.That(ViewHelpers.DeadlineText(past), Is.EqualTo("overdue"));
    }

    [Test]
    public void DeadlineText_DueToday_BeforeMidnight()
    {
        // End of today — 1 minute before midnight local time
        var endOfToday = DateTimeOffset.Now.Date.AddDays(1).AddMinutes(-1);
        var deadline = new DateTimeOffset(endOfToday, DateTimeOffset.Now.Offset);

        Assert.That(ViewHelpers.DeadlineText(deadline), Is.EqualTo("due today"));
    }

    [Test]
    public void DeadlineText_DueTomorrow_JustAfterMidnight()
    {
        // 1 minute into tomorrow
        var startOfTomorrow = DateTimeOffset.Now.Date.AddDays(1).AddMinutes(1);
        var deadline = new DateTimeOffset(startOfTomorrow, DateTimeOffset.Now.Offset);

        Assert.That(ViewHelpers.DeadlineText(deadline), Is.EqualTo("due tomorrow"));
    }

    [Test]
    public void DeadlineText_DueTomorrow_EndOfTomorrow()
    {
        // End of tomorrow — 1 minute before midnight the day after
        var endOfTomorrow = DateTimeOffset.Now.Date.AddDays(2).AddMinutes(-1);
        var deadline = new DateTimeOffset(endOfTomorrow, DateTimeOffset.Now.Offset);

        Assert.That(ViewHelpers.DeadlineText(deadline), Is.EqualTo("due tomorrow"));
    }

    [Test]
    public void DeadlineText_DueWithinSixDays_ShowsWeekday()
    {
        var fourDaysFromNow = DateTimeOffset.Now.Date.AddDays(4);
        var deadline = new DateTimeOffset(fourDaysFromNow, DateTimeOffset.Now.Offset);
        var expected = $"due {deadline:dddd}";

        Assert.That(ViewHelpers.DeadlineText(deadline), Is.EqualTo(expected));
    }

    [Test]
    public void DeadlineText_DueInMoreThanSixDays_ShowsTimeUntil()
    {
        var future = DateTimeOffset.Now.AddDays(10);

        Assert.That(ViewHelpers.DeadlineText(future), Does.StartWith("in "));
    }

    [Test]
    public void DeadlineText_Null_ReturnsNever()
    {
        Assert.That(ViewHelpers.DeadlineText(null), Is.EqualTo("never"));
    }

    // move to base class?

    private static TimeSpan _timeStep = TimeSpan.FromMinutes(1);

    private DateTimeOffset t(int i) =>
        new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero).Add(_timeStep * i);

    private static DateTimeOffset d(int days) =>
        new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero).AddDays(days);
}
