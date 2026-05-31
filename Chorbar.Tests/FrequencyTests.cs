using Chorbar.Model;

namespace Chorbar.Tests;

public class FrequencyTests : TestFixture
{
    [Test]
    public void Simple()
    {
        var subject = new Chore(t(0), [ht(1), ht(2)]);

        Assert.That(subject.Frequency(), Is.EqualTo(TimeSpan.FromMinutes(1)));
    }

    [Test]
    public void Irregular()
    {
        var subject = new Chore(t(0), [ht(5), ht(15), ht(25)]);

        Assert.That(subject.Frequency(), Is.EqualTo(TimeSpan.FromMinutes(10)));
    }

    [Test]
    public void Empty()
    {
        var subject = new Chore(t(0), []);

        Assert.That(subject.Frequency(), Is.EqualTo(TimeSpan.Zero));
    }
}
