namespace Chorbar.Utils;

public interface IAsyncLazyCache<TKey, TValue>
    where TKey : notnull
{
    Task<TValue> GetOrAddAsync(
        TKey key,
        Func<TKey, CancellationToken, Task<TValue>> factory,
        CancellationToken cancellationToken
    );

    void Remove(TKey key);
}
