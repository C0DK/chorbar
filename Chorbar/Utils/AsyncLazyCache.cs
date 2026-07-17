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
                () =>
                {
                    Task<TValue> task;
                    try
                    {
                        task = factory(k, cancellationToken);
                    }
                    catch
                    {
                        _cache.TryRemove(k, out _);
                        throw;
                    }
                    return EvictOnFailure(k, task);
                },
                LazyThreadSafetyMode.ExecutionAndPublication
            )
        );
        try
        {
            return lazy.Value;
        }
        catch
        {
            _cache.TryRemove(key, out _);
            throw;
        }
    }

    public void Remove(TKey key) => _cache.TryRemove(key, out _);

    private async Task<TValue> EvictOnFailure(TKey key, Task<TValue> task)
    {
        try
        {
            return await task;
        }
        catch
        {
            _cache.TryRemove(key, out _);
            throw;
        }
    }
}
