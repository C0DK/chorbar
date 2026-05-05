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

    public static NpgsqlCommand CreateCommand(
        this NpgsqlDataSource db,
        string sql,
        Action<NpgsqlParameterCollection> bind,
        int? commandTimeout = null
    )
    {
        var command = db.CreateCommand(sql);
        bind(command.Parameters);
        if (commandTimeout is not null)
            command.CommandTimeout = commandTimeout.Value;
        return command;
    }

    public static NpgsqlCommand CreateCommand(
        this NpgsqlConnection connection,
        string sql,
        Action<NpgsqlParameterCollection> bind,
        int? commandTimeout = null
    )
    {
        var command = connection.CreateCommand(sql, commandTimeout);
        bind(command.Parameters);
        return command;
    }

    public static NpgsqlCommand CreateCommand(
        this NpgsqlConnection connection,
        string sql,
        int? commandTimeout = null
    )
    {
        var command = new NpgsqlCommand(sql, connection);

        if (commandTimeout is not null)
            command.CommandTimeout = commandTimeout.Value;
        return command;
    }

    public static async ValueTask ExecuteAsync(
        this NpgsqlConnection connection,
        string sql,
        Action<NpgsqlParameterCollection> bind,
        CancellationToken cancellationToken,
        int? commandTimeout = null
    )
    {
        await using var command = connection.CreateCommand(sql, bind);
        if (commandTimeout is not null)
            command.CommandTimeout = commandTimeout.Value;
        await command.ExecuteNonQueryAsync();
    }

    public static ValueTask ExecuteAsync(
        this NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken,
        int? commandTimeout = null
    ) => connection.ExecuteAsync(sql, p => { }, cancellationToken, commandTimeout);
}
