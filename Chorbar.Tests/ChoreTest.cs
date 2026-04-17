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

    // move to base class?

    private static TimeSpan _timeStep = TimeSpan.FromMinutes(1);

    private DateTimeOffset t(int i) =>
        new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero).Add(_timeStep * i);
}
