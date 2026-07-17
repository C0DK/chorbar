using Chorbar.Utils;

namespace Chorbar.Tests.Unit;

public class AsyncLazyCacheTest
{
    [Test]
    public async Task FailedFactoryIsNotCached()
    {
        var cache = new AsyncLazyCache<string, int>();
        var calls = 0;
        var key = "k";

        Task<int> Failing()
        {
            calls++;
            return Task.FromException<int>(new InvalidOperationException("boom"));
        }

        for (var i = 0; i < 3; i++)
        {
            try
            {
                await cache.GetOrAddAsync(key, (_, _) => Failing(), CancellationToken.None);
                Assert.Fail("expected exception");
            }
            catch (InvalidOperationException) { }
        }

        Assert.That(calls, Is.EqualTo(3));
    }

    [Test]
    public async Task SuccessfulResultIsCached()
    {
        var cache = new AsyncLazyCache<string, int>();
        var calls = 0;

        Task<int> Factory(string _, CancellationToken __)
        {
            calls++;
            return Task.FromResult(42);
        }

        var a = await cache.GetOrAddAsync("k", Factory, CancellationToken.None);
        var b = await cache.GetOrAddAsync("k", Factory, CancellationToken.None);

        Assert.That(a, Is.EqualTo(42));
        Assert.That(b, Is.EqualTo(42));
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public async Task FailedFactoryEvictsAndNextCallSucceeds()
    {
        var cache = new AsyncLazyCache<string, int>();
        var calls = 0;
        var shouldFail = true;

        Task<int> Factory(string _, CancellationToken __)
        {
            calls++;
            return shouldFail
                ? Task.FromException<int>(new InvalidOperationException("boom"))
                : Task.FromResult(7);
        }

        for (var i = 0; i < 3; i++)
        {
            try
            {
                await cache.GetOrAddAsync("k", Factory, CancellationToken.None);
                Assert.Fail("expected exception");
            }
            catch (InvalidOperationException) { }
        }

        Assert.That(calls, Is.EqualTo(3));

        shouldFail = false;
        var ok = await cache.GetOrAddAsync("k", Factory, CancellationToken.None);
        Assert.That(ok, Is.EqualTo(7));
        Assert.That(calls, Is.EqualTo(4));
    }
}
