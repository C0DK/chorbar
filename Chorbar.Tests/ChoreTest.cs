using Chorbar.Controllers;
using Chorbar.Model;

namespace Chorbar.Tests;

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
    public void Streak_EmptyHistory_ReturnsNull()
    {
        var subject = new Chore(d(0), []);

        Assert.That(subject.Streak(d(10)), Is.Null);
    }

    [Test]
    public void Streak_SingleCompletion_WithinWindow_ReturnsDaysSinceCompletion()
    {
        // created day 0, done day 7, frequency = 7 days, maxGap = 7.7 days
        // now = day 14 → streak active, started at d(7) → 7 days
        var subject = new Chore(d(0), [d(7)]);

        Assert.That(subject.Streak(d(14)), Is.EqualTo(new Streak(7, DateUnit.Day)));
    }

    [Test]
    public void Streak_SingleCompletion_TooOld_ReturnsNull()
    {
        // now = day 16 → timeSinceLast = 9 days > 7.7 → streak broken
        var subject = new Chore(d(0), [d(7)]);

        Assert.That(subject.Streak(d(16)), Is.Null);
    }

    [Test]
    public void Streak_MultipleConsecutive_ReturnsDaysSinceFirst()
    {
        // streak started at d(7), now = d(21) → 14 days
        var subject = new Chore(d(0), [d(7), d(14), d(21)]);

        Assert.That(subject.Streak(d(21)), Is.EqualTo(new Streak(14, DateUnit.Day)));
    }

    [Test]
    public void Streak_BrokenInMiddle_CountsFromRestartOnly()
    {
        // streak breaks between d(14) and d(44), restarts at d(44), now = d(51) → 7 days
        var subject = new Chore(d(0), [d(7), d(14), d(44), d(51)]);

        Assert.That(subject.Streak(d(51)), Is.EqualTo(new Streak(7, DateUnit.Day)));
    }

    [Test]
    public void Streak_ShortFrequency_OneDayLatency_ActiveStreak()
    {
        // frequency = 5 days, maxGap = 6 days, streak started at d(0), now = d(16) → 16 days
        var subject = new Chore(d(-5), [d(0), d(5), d(10), d(16)]);

        Assert.That(subject.Streak(d(16)), Is.EqualTo(new Streak(16, DateUnit.Day)));
    }

    [Test]
    public void Streak_ShortFrequency_BreaksWhenTooLate()
    {
        // gap d(10)→d(17) = 7 days > maxGap 6 → streak broken
        var subject = new Chore(d(-5), [d(0), d(5), d(10), d(17)]);

        Assert.That(subject.Streak(d(17)), Is.Null);
    }

    [Test]
    public void Streak_HundredDayFrequency_TenDayLatency()
    {
        // frequency ≈ 100 days, maxGap = 110 days, streak started at d(100), now = d(300) → 200 days
        var subject = new Chore(d(0), [d(100), d(200), d(300)]);

        Assert.That(subject.Streak(d(300)), Is.EqualTo(new Streak(200, DateUnit.Day)));
    }

    [Test]
    public void Streak_HundredDayFrequency_BreaksAfterElevenDaysLate()
    {
        // gap d(200)→d(311) = 111 days > 110 → streak broken
        var subject = new Chore(d(0), [d(100), d(200), d(311)]);

        Assert.That(subject.Streak(d(311)), Is.Null);
    }

    [Test]
    public void Streak_GoalInWeeks_NumeratorInWeeks()
    {
        // streak started at d(7), now = d(21) → 14 days → 2w
        var subject = new Chore(d(0), [d(7), d(14), d(21)], new Goal(1, DateUnit.Week));

        Assert.That(subject.Streak(d(21)), Is.EqualTo(new Streak(2, DateUnit.Week)));
    }

    [Test]
    public void Streak_GoalInMonths_NumeratorInMonths()
    {
        // streak started at d(30), now = d(90) → 60 days → 2m
        var subject = new Chore(d(0), [d(30), d(60), d(90)], new Goal(1, DateUnit.Month));

        Assert.That(subject.Streak(d(90)), Is.EqualTo(new Streak(2, DateUnit.Month)));
    }

    [Test]
    public void Streak_ToString_Days() =>
        Assert.That(new Streak(10, DateUnit.Day).ToString(), Is.EqualTo("10d"));

    [Test]
    public void Streak_ToString_Weeks() =>
        Assert.That(new Streak(3, DateUnit.Week).ToString(), Is.EqualTo("3w"));

    [Test]
    public void Streak_ToString_Months() =>
        Assert.That(new Streak(2, DateUnit.Month).ToString(), Is.EqualTo("2m"));

    [Test]
    public void Streak_ToString_Years() =>
        Assert.That(new Streak(1, DateUnit.Year).ToString(), Is.EqualTo("1y"));

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
