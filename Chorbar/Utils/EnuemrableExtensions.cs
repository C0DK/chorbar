namespace Chorbar.Utils;

public static class EnuemrableExtensions
{
    public static async Task<List<TOut>> SelectAsync<TIn, TOut>(
        this IEnumerable<TIn> source,
        Func<TIn, Task<TOut>> selector
    )
    {
        var list = new List<TOut>();
        foreach (var item in source)
            list.Add(await selector(item));
        return list;
    }
}
