using System.Data.Common;
using System.Runtime.CompilerServices;
using Npgsql;

namespace Chorbar;

public static class NpgsqlExtensions
{
    public static string? GetStringOrNull(this DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<string>(ordinal);

    public static ValueTask<T> FirstAsync<T>(
        this NpgsqlCommand command,
        Func<DbDataReader, T> selector,
        CancellationToken cancellationToken
    ) => command.ReadAllAsync(selector, cancellationToken).FirstAsync(cancellationToken);

    public static ValueTask<T?> FirstOrDefaultAsync<T>(
        this NpgsqlCommand command,
        Func<DbDataReader, T> selector,
        CancellationToken cancellationToken
    ) => command.ReadAllAsync(selector, cancellationToken).FirstOrDefaultAsync(cancellationToken);

    public static async IAsyncEnumerable<T> ReadAllAsync<T>(
        this NpgsqlCommand command,
        Func<DbDataReader, T> selector,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync(cancellationToken))
        {
            yield return selector(reader);
        }
    }
}
