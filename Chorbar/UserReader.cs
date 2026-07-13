using System.Diagnostics;
using Chorbar.Model;
using Chorbar.Utils;
using Npgsql;

namespace Chorbar;

public class UserReader
{
    public const string ActivitySourceName = "Chorbar.UserStoreReader";
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private readonly NpgsqlConnection _connection;
    private readonly IAsyncLazyCache<Email, UserInfo> _infoCache;

    public UserReader(NpgsqlConnection connection, IAsyncLazyCache<Email, UserInfo> infoCache)
    {
        _connection = connection;
        _infoCache = infoCache;
    }

    public async ValueTask<string> DisplayName(Email identity, CancellationToken cancellationToken)
    {
        var info = await Info(identity, cancellationToken);
        return info.DisplayName;
    }

    public ValueTask<UserInfo> Info(Email identity, CancellationToken cancellationToken)
    {
        var task = _infoCache.GetOrAddAsync(
            identity,
            (key, ct) => ReadInfoCore(key, ct).AsTask(),
            cancellationToken
        );
        return new ValueTask<UserInfo>(task);
    }

    private async ValueTask<UserInfo> ReadInfoCore(
        Email identity,
        CancellationToken cancellationToken
    )
    {
        using var activity = ActivitySource.StartActivity("UserReader.Info");
        activity?.SetTag("email", identity.Value);
        await using var enumerator = ReadEvents(identity, cancellationToken);
        var entity = UserInfo.Create(identity);

        while (await enumerator.MoveNextAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            entity = enumerator.Current.Apply(entity);
        }

        return entity;
    }

    public async IAsyncEnumerator<UserEvent> ReadEvents(
        Email identity,
        CancellationToken cancellationToken
    )
    {
        using var command = new NpgsqlCommand(
            //language=sql
            """
            SELECT
              email,
              version,
              timestamp,
              payload
            FROM user_event
            WHERE email = $1
            ORDER BY timestamp
            """,
            _connection
        );
        command.Parameters.AddWithValue(identity.Value);
        await using var enumerator = command
            .ReadAllAsync(
                async (reader, ct) =>
                    new UserEvent(
                        Email: identity,
                        Version: await reader.GetFieldValueAsync<int>(1, ct),
                        Timestamp: await reader.GetFieldValueAsync<DateTimeOffset>(2, ct),
                        Payload: UserEventPayload.Deserialize(
                            await reader.GetFieldValueAsync<string>(3, ct)
                        )
                    ),
                cancellationToken
            )
            .GetAsyncEnumerator(cancellationToken);

        while (await enumerator.MoveNextAsync())
        {
            yield return enumerator.Current;
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
