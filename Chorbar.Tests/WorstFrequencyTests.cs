using Chorbar.Model;

namespace Chorbar.Tests;

public class WorstFrequencyTests : TestFixture
{
    [Test]
    public void Simple()
    {
        var subject = new Chore(t(0), [ht(1), ht(2)]);

        Assert.That(subject.WorstFrequency(), Is.EqualTo(TimeSpan.FromMinutes(1)));
    }

    [Test]
    public void Irregular()
    {
        var subject = new Chore(t(0), [ht(2), ht(15), ht(17)]);

        Assert.That(subject.WorstFrequency(), Is.EqualTo(TimeSpan.FromMinutes(13)));
    }

    [Test]
    public void Empty()
    {
        var subject = new Chore(t(0), []);

        Assert.That(subject.WorstFrequency(), Is.EqualTo(TimeSpan.Zero));
    }
}
