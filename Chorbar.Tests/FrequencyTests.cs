using Chorbar.Model;

namespace Chorbar.Tests;

public class FrequencyTests : TestFixture
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
