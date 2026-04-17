using Chorbar.Model;

namespace Chorbar.Tests;

[TestFixture]
public class EmailTests
{
    [TestCase("user@example.com")]
    [TestCase("User@Example.COM")]
    [TestCase("first.last@sub.domain.org")]
    [TestCase("user+tag@example.com")]
    public void TryParse_ValidEmail_ReturnsTrue(string input)
    {
        var result = Email.TryParse(input, out var email);
        Assert.That(result, Is.True);
        Assert.That(email.Value, Is.EqualTo(input.ToLowerInvariant()));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("notanemail")]
    [TestCase("missing@")]
    [TestCase("@nodomain.com")]
    [TestCase("user@example.")]
    public void TryParse_InvalidEmail_ReturnsFalse(string? input)
    {
        var result = Email.TryParse(input, out var email);
        Assert.That(result, Is.False);
        Assert.That(email, Is.EqualTo(default(Email)));
    }

    [Test]
    public void TryParse_NormalizesToLowercase()
    {
        Email.TryParse("USER@EXAMPLE.COM", out var email);
        Assert.That(email.Value, Is.EqualTo("user@example.com"));
    }

    [Test]
    public void Parse_ValidEmail_ReturnsEmail()
    {
        var email = Email.Parse("user@example.com");
        Assert.That(email.Value, Is.EqualTo("user@example.com"));
    }

    [Test]
    public void Parse_InvalidEmail_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Email.Parse("bad-email"));
    }

    [Test]
    public void ToString_ReturnsValue()
    {
        var email = Email.Parse("user@example.com");
        Assert.That(email.ToString(), Is.EqualTo("user@example.com"));
    }

    [Test]
    public void ImplicitStringConversion_ReturnsValue()
    {
        var email = Email.Parse("user@example.com");
        string s = email;
        Assert.That(s, Is.EqualTo("user@example.com"));
    }

    [Test]
    public void Equality_SameValue_AreEqual()
    {
        var a = Email.Parse("user@example.com");
        var b = Email.Parse("user@example.com");
        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void Equality_DifferentCasing_AreEqualAfterNormalization()
    {
        var a = Email.Parse("User@Example.COM");
        var b = Email.Parse("user@example.com");
        Assert.That(a, Is.EqualTo(b));
    }
}
