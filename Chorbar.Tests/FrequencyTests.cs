using Chorbar.Model;

namespace Chorbar.Tests;

public class FrequencyTests : TestFixture
{
    [Test]
    public void EmptyHistory_ReturnsZero()
    {
        var subject = new Chore(d(0), []);

        Assert.That(subject.Frequency(d(0), DateUnit.Day), Is.EqualTo(new Frequency(0m, DateUnit.Day)));
    }

    [Test]
    public void DailyFrequency_Simple()
    {
        var subject = new Chore(d(0), [h(2), h(4), h(6), h(8)]);

        var result = subject.Frequency(d(0), DateUnit.Day);

        Assert.That(result.Numerator, Is.EqualTo(0.5m).Within(0.001m));
        Assert.That(result.Unit, Is.EqualTo(DateUnit.Day));
    }

    [Test]
    public void WeeklyFrequency()
    {
        var subject = new Chore(d(0), [h(7), h(14), h(21), h(28)]);

        var result = subject.Frequency(d(0), DateUnit.Week);

        Assert.That(result.Numerator, Is.EqualTo(1.0m).Within(0.001m));
        Assert.That(result.Unit, Is.EqualTo(DateUnit.Week));
    }

    [Test]
    public void MonthlyFrequency()
    {
        var subject = new Chore(d(0), [h(30), h(60), h(90)]);

        var result = subject.Frequency(d(0), DateUnit.Month);

        var expectedDays = 90.0;
        var months = expectedDays / (365.25 / 12);
        var expected = 3.0 / months;
        Assert.That((double)result.Numerator, Is.EqualTo(expected).Within(0.001));
        Assert.That(result.Unit, Is.EqualTo(DateUnit.Month));
    }

    [Test]
    public void StartDateAfterLastDone_ReturnsZero()
    {
        var subject = new Chore(d(0), [h(2), h(4)]);

        Assert.That(subject.Frequency(d(10), DateUnit.Day), Is.EqualTo(new Frequency(0m, DateUnit.Day)));
    }

    [Test]
    public void Overload_UsesGoalUnit()
    {
        var subject = new Chore(d(0), [h(7), h(14), h(21), h(28)], new Goal(2, DateUnit.Week));

        var result = subject.Frequency();

        Assert.That(result.Numerator, Is.EqualTo(1.0m).Within(0.001m));
        Assert.That(result.Unit, Is.EqualTo(DateUnit.Week));
    }

    [Test]
    public void Overload_NoGoal_FallsBackToDay()
    {
        var subject = new Chore(d(0), [h(2), h(4), h(6), h(8)]);

        var result = subject.Frequency();

        Assert.That(result.Numerator, Is.EqualTo(0.5m).Within(0.001m));
        Assert.That(result.Unit, Is.EqualTo(DateUnit.Day));
    }

    [Test]
    public void SingleCompletion()
    {
        var subject = new Chore(d(0), [h(10)]);

        var result = subject.Frequency(d(0), DateUnit.Day);

        Assert.That(result.Numerator, Is.EqualTo(0.1m).Within(0.001m));
        Assert.That(result.Unit, Is.EqualTo(DateUnit.Day));
    }

    [Test]
    public void ToString_FormatsAsRateWithUnitLetter()
    {
        var freq = new Frequency(0.5m, DateUnit.Day);

        Assert.That(freq.ToString(), Is.EqualTo("0.5/d"));
    }
}
