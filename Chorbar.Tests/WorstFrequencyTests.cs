using Chorbar.Model;

namespace Chorbar.Tests;

public class WorstFrequencyTests : TestFixture
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
