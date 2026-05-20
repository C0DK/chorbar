using Chorbar.Controllers;
using Chorbar.Model;

namespace Chorbar.Tests;

public class ChoreTest
{
    public class Frequency
    {
        [Test]
        public void Simple()
        {
            var subject = new Chore(t(0), [t(1), t(2)]);

            Assert.That(subject.Frequency(), Is.EqualTo(TimeSpan.FromMinutes(1)));
        }

        [Test]
        public void Irregular()
        {
            var subject = new Chore(t(0), [t(5), t(15), t(25)]);

            Assert.That(subject.Frequency(), Is.EqualTo(TimeSpan.FromMinutes(10)));
        }

        [Test]
        public void Empty()
        {
            var subject = new Chore(t(0), []);

            Assert.That(subject.Frequency(), Is.EqualTo(TimeSpan.Zero));
        }
    }

    public class WorstFrequency
    {
        [Test]
        public void Simple()
        {
            var subject = new Chore(t(0), [t(1), t(2)]);

            Assert.That(subject.WorstFrequency(), Is.EqualTo(TimeSpan.FromMinutes(1)));
        }

        [Test]
        public void Irregular()
        {
            var subject = new Chore(t(0), [t(2), t(15), t(17)]);

            Assert.That(subject.WorstFrequency(), Is.EqualTo(TimeSpan.FromMinutes(13)));
        }

        [Test]
        public void Empty()
        {
            var subject = new Chore(t(0), []);

            Assert.That(subject.WorstFrequency(), Is.EqualTo(TimeSpan.Zero));
        }
    }

    public class StreakTests
    {
        [Test]
        public void EmptyHistory_ReturnsNull()
        {
            var subject = new Chore(d(0), []);

            Assert.That(subject.Streak(d(10)), Is.Null);
        }

        [Test]
        public void OneBefore2Streak()
        {
            var subject = new Chore(d(0), [d(0), d(5)], Goal: new Goal(5, DateUnit.Day));

            Assert.That(subject.Streak(d(7)), Is.Null);
        }

        [Test]
        public void WithinGoal()
        {
            var subject = new Chore(d(0), [d(0), d(5), d(10)], Goal: new Goal(5, DateUnit.Day));

            Assert.That(subject.Streak(d(11)), Is.EqualTo(new Streak(11, DateUnit.Day)));
        }

        [Test]
        public void WithinGoalByLatency()
        {
            var subject = new Chore(d(0), [d(0), d(5), d(10)], Goal: new Goal(5, DateUnit.Day));

            Assert.That(subject.Streak(d(16)), Is.EqualTo(new Streak(16, DateUnit.Day)));
        }

        [Test]
        public void StreakBroken()
        {
            var subject = new Chore(d(0), [d(0), d(5), d(10)], Goal: new Goal(5, DateUnit.Day));

            Assert.That(subject.Streak(d(17)), Is.Null);
        }

        [Test]
        public void BrokenInMiddle_CountsFromRestartOnly()
        {
            var subject = new Chore(
                d(0),
                [
                    d(0),
                    d(5),
                    d(10),
                    // broken!
                    d(20),
                    d(25),
                    d(30),
                ],
                Goal: new Goal(5, DateUnit.Day)
            );

            Assert.That(subject.Streak(d(31)), Is.EqualTo(new Streak(11, DateUnit.Day)));
        }

        [Test]
        public void GoalInWeeks_NumeratorInWeeks()
        {
            var subject = new Chore(d(0), [d(7), d(14), d(21)], new Goal(1, DateUnit.Week));

            Assert.That(subject.Streak(d(21)), Is.EqualTo(new Streak(2, DateUnit.Week)));
        }

        [Test]
        public void GoalInMonths_NumeratorInMonths()
        {
            // streak started at d(30), now = d(90) → 60 days → 2m
            var subject = new Chore(d(0), [d(30), d(60), d(90)], new Goal(1, DateUnit.Month));

            Assert.That(subject.Streak(d(90)), Is.EqualTo(new Streak(2, DateUnit.Month)));
        }
    }

    public class StreakToString
    {
        [Test]
        public void Days() =>
            Assert.That(new Streak(10, DateUnit.Day).ToString(), Is.EqualTo("10d"));

        [Test]
        public void Weeks() =>
            Assert.That(new Streak(3, DateUnit.Week).ToString(), Is.EqualTo("3w"));

        [Test]
        public void Months() =>
            Assert.That(new Streak(2, DateUnit.Month).ToString(), Is.EqualTo("2m"));

        [Test]
        public void Years() =>
            Assert.That(new Streak(1, DateUnit.Year).ToString(), Is.EqualTo("1y"));
    }

    public class DeadlineText
    {
        [Test]
        public void Overdue()
        {
            var past = DateTimeOffset.Now.AddHours(-1);

            Assert.That(ViewHelpers.DeadlineText(past), Is.EqualTo("overdue"));
        }

        [Test]
        public void DueToday_BeforeMidnight()
        {
            var endOfToday = DateTimeOffset.Now.Date.AddDays(1).AddMinutes(-1);
            var deadline = new DateTimeOffset(endOfToday, DateTimeOffset.Now.Offset);

            Assert.That(ViewHelpers.DeadlineText(deadline), Is.EqualTo("due today"));
        }

        [Test]
        public void DueTomorrow_JustAfterMidnight()
        {
            var startOfTomorrow = DateTimeOffset.Now.Date.AddDays(1).AddMinutes(1);
            var deadline = new DateTimeOffset(startOfTomorrow, DateTimeOffset.Now.Offset);

            Assert.That(ViewHelpers.DeadlineText(deadline), Is.EqualTo("due tomorrow"));
        }

        [Test]
        public void DueTomorrow_EndOfTomorrow()
        {
            var endOfTomorrow = DateTimeOffset.Now.Date.AddDays(2).AddMinutes(-1);
            var deadline = new DateTimeOffset(endOfTomorrow, DateTimeOffset.Now.Offset);

            Assert.That(ViewHelpers.DeadlineText(deadline), Is.EqualTo("due tomorrow"));
        }

        [Test]
        public void DueWithinSixDays_ShowsWeekday()
        {
            var fourDaysFromNow = DateTimeOffset.Now.Date.AddDays(4);
            var deadline = new DateTimeOffset(fourDaysFromNow, DateTimeOffset.Now.Offset);
            var expected = $"due {deadline:dddd}";

            Assert.That(ViewHelpers.DeadlineText(deadline), Is.EqualTo(expected));
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

    private static readonly TimeSpan _timeStep = TimeSpan.FromMinutes(1);

    private static DateTimeOffset t(int i) =>
        new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero).Add(_timeStep * i);

    private static DateTimeOffset d(int days) =>
        new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero).AddDays(days);
}
