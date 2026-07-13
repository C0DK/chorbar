using System.Collections.Concurrent;

namespace Chorbar.Utils;

public class AsyncLazyCache<TKey, TValue> : IAsyncLazyCache<TKey, TValue>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, Lazy<Task<TValue>>> _cache = new();

    public Task<TValue> GetOrAddAsync(
        TKey key,
        Func<TKey, CancellationToken, Task<TValue>> factory,
        CancellationToken cancellationToken
    )
    {
        var lazy = _cache.GetOrAdd(
            key,
            k => new Lazy<Task<TValue>>(
                () => factory(k, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication
            )
        );
        return lazy.Value;
    }

    public void Remove(TKey key) => _cache.TryRemove(key, out _);
}
