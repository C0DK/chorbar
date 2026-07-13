using Chorbar.Model;
using Chorbar.Utils;
using Npgsql;

namespace Chorbar.Tests.Integration;

public class UserStoreCacheTest
{
    NpgsqlConnection _conn = null!;

    [SetUp]
    public async Task SetUp()
    {
        _conn = await DatabaseFixture.DataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("TRUNCATE user_event", _conn);
        await cmd.ExecuteNonQueryAsync();
    }

    [TearDown]
    public async Task TearDown() => await _conn.DisposeAsync();

    [Test, CancelAfter(10_000)]
    public async Task ReadInfoHitsCache(CancellationToken cancellationToken)
    {
        var spy = new SpyCache();
        var store = new UserStore(
            _conn,
            new StaticIdentityProvider(_emailAlice),
            TimeProvider.System,
            spy
        );

        await store.ReadInfo(cancellationToken);

        Assert.That(spy.GetOrAddInvocations, Is.EqualTo(1));
    }

    [Test, CancelAfter(10_000)]
    public async Task WriteSetDisplayNameClearsCache(CancellationToken cancellationToken)
    {
        var spy = new SpyCache();
        var store = new UserStore(
            _conn,
            new StaticIdentityProvider(_emailAlice),
            TimeProvider.System,
            spy
        );

        await store.Write(new SetDisplayName("Alice"), cancellationToken);

        Assert.That(spy.RemoveInvocations, Is.EqualTo(1));
        Assert.That(spy.LastRemovedKey, Is.EqualTo(_emailAlice));
    }

    [Test, CancelAfter(10_000)]
    public async Task CacheDeduplicatesFactoryCalls(CancellationToken cancellationToken)
    {
        var spy = new SpyCache();
        var store = new UserStore(
            _conn,
            new StaticIdentityProvider(_emailAlice),
            TimeProvider.System,
            spy
        );

        await store.ReadInfo(cancellationToken);
        await store.ReadInfo(cancellationToken);

        Assert.That(spy.GetOrAddInvocations, Is.EqualTo(2));
        Assert.That(spy.FactoryInvocations, Is.EqualTo(1));
    }

    [Test, CancelAfter(10_000)]
    public async Task CacheClearThenReadReinvokesFactory(CancellationToken cancellationToken)
    {
        var spy = new SpyCache();
        var store = new UserStore(
            _conn,
            new StaticIdentityProvider(_emailAlice),
            TimeProvider.System,
            spy
        );

        await store.ReadInfo(cancellationToken);
        await store.Write(new SetDisplayName("Alice"), cancellationToken);
        await store.ReadInfo(cancellationToken);

        Assert.That(spy.FactoryInvocations, Is.EqualTo(2));
        Assert.That(spy.RemoveInvocations, Is.EqualTo(1));
    }

    private static Email _emailAlice => new Email("alice@example.com");

    private class SpyCache : IAsyncLazyCache<Email, UserInfo>
    {
        public int GetOrAddInvocations = 0;
        public int FactoryInvocations = 0;
        public int RemoveInvocations = 0;
        public Email? LastRemovedKey;
        private UserInfo? _cached;

        public async Task<UserInfo> GetOrAddAsync(
            Email key,
            Func<Email, CancellationToken, Task<UserInfo>> factory,
            CancellationToken cancellationToken
        )
        {
            GetOrAddInvocations++;
            if (_cached is not null)
                return _cached;

            FactoryInvocations++;
            _cached = await factory(key, cancellationToken);
            return _cached;
        }

        public void Remove(Email key)
        {
            RemoveInvocations++;
            LastRemovedKey = key;
            _cached = null;
        }
    }
}
