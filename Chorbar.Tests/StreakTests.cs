using Chorbar.Model;

namespace Chorbar.Tests;

public class StreakTests : TestFixture
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
        var subject = new Chore(d(0), [h(0), h(5)], Goal: new Goal(5, DateUnit.Day));

        Assert.That(subject.Streak(d(7)), Is.Null);
    }

    [Test]
    public void WithinGoal()
    {
        var subject = new Chore(d(0), [h(0), h(5), h(10)], Goal: new Goal(5, DateUnit.Day));

        Assert.That(subject.Streak(d(11)), Is.EqualTo(new Streak(11, DateUnit.Day)));
    }

    [Test]
    public void WithinGoalByLatency()
    {
        var subject = new Chore(d(0), [h(0), h(5), h(10)], Goal: new Goal(5, DateUnit.Day));

        Assert.That(subject.Streak(d(16)), Is.EqualTo(new Streak(16, DateUnit.Day)));
    }

    [Test]
    public void StreakBroken()
    {
        var subject = new Chore(d(0), [h(0), h(5), h(10)], Goal: new Goal(5, DateUnit.Day));

        // a bit of leway
        Assert.That(subject.Streak(d(18)), Is.Null);
    }

    [Test]
    public void BrokenInMiddle_CountsFromRestartOnly()
    {
        var subject = new Chore(
            d(0),
            [
                h(0),
                h(5),
                h(10),
                // broken!
                h(20),
                h(25),
                h(30),
            ],
            Goal: new Goal(5, DateUnit.Day)
        );

        Assert.That(subject.Streak(d(31)), Is.EqualTo(new Streak(11, DateUnit.Day)));
    }

    [Test]
    public void GoalInWeeks_NumeratorInWeeks()
    {
        var subject = new Chore(d(0), [h(7), h(14), h(21)], new Goal(1, DateUnit.Week));

        Assert.That(subject.Streak(d(21)), Is.EqualTo(new Streak(2, DateUnit.Week)));
    }

    [Test]
    public void GoalInMonths_NumeratorInMonths()
    {
        // streak started at d(30), now = d(90) → 60 days → 2m
        var subject = new Chore(d(0), [h(30), h(60), h(90)], new Goal(1, DateUnit.Month));

        Assert.That(subject.Streak(d(90)), Is.EqualTo(new Streak(2, DateUnit.Month)));
    }

    [Test]
    public void DeadlineIsEndOfDay()
    {
        var subject = new Chore(
            new DateTimeOffset(2026, 01, 01, 00, 00, 00, TimeSpan.Zero),
            [
                (new DateTimeOffset(2026, 05, 19, 19, 00, 00, TimeSpan.Zero), "test@test.dk"),
                (new DateTimeOffset(2026, 05, 22, 16, 00, 00, TimeSpan.Zero), "test@test.dk"),
                (new DateTimeOffset(2026, 05, 26, 19, 00, 00, TimeSpan.Zero), "test@test.dk"),
            ],
            new Goal(3, DateUnit.Day)
        );

        Assert.That(
            subject.Streak(new DateTimeOffset(2026, 05, 27, 12, 00, 00, TimeSpan.Zero)),
            Is.EqualTo(new Streak(7, DateUnit.Day))
        );
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
}
