using System.Diagnostics;
using Chorbar.Model;
using Npgsql;
using NpgsqlTypes;

namespace Chorbar;

public class UserStore
{
    public const string ActivitySourceName = "Chorbar.UserStore";
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public UserStore(NpgsqlConnection connection, IIdentityProvider identityProvider)
    {
        _connection = connection;
        _identityProvider = identityProvider;
        _timeProvider = TimeProvider.System;
    }

    public UserStore(
        NpgsqlConnection connection,
        IIdentityProvider identityProvider,
        TimeProvider timeProvider
    )
    {
        _connection = connection;
        _identityProvider = identityProvider;
        _timeProvider = timeProvider;
    }

    public ValueTask<UserSettings> Write(
        UserEventPayload payload,
        CancellationToken cancellationToken
    ) => Write([payload], cancellationToken);

    public async ValueTask<UserSettings> Write(
        IEnumerable<UserEventPayload> payloads,
        CancellationToken cancellationToken
    )
    {
        using var activity = ActivitySource.StartActivity("UserStore.Write");
        var identity = _identityProvider.GetIdentity();
        activity?.SetTag("email", identity.Value);
        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken);
        foreach (var payload in payloads)
        {
            await WriteEvent(payload, cancellationToken);
        }
        var updatedEntity = await ReadSettings(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return updatedEntity;
    }

    private async ValueTask WriteEvent(
        UserEventPayload payload,
        CancellationToken cancellationToken
    )
    {
        var identity = _identityProvider.GetIdentity();
        var entity = await ReadSettings(cancellationToken);
        var now = _timeProvider.GetUtcNow();
        if (!payload.IsValid(entity))
            throw new InvalidOperationException(
                $"Event '{payload.EventKind}' not valid! ({payload})"
            );
        await _connection.ExecuteAsync(
            //language=sql
            """
            INSERT INTO
              user_event(email, version, timestamp, payload)
            VALUES($1, $2, $3, $4)
            """,
            p =>
            {
                p.AddWithValue(identity.Value);
                p.AddWithValue((entity.History.LastOrDefault()?.Version ?? 0) + 1);
                p.AddWithValue(NpgsqlDbType.TimestampTz, now);
                p.AddWithValue(NpgsqlDbType.Jsonb, payload.Serialize());
            },
            cancellationToken
        );
    }

    public async ValueTask<UserSettings> ReadSettings(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("HouseholdStore.Read");

        // TODO: precheck to save resources?
        var identity = _identityProvider.GetIdentity();
        activity?.SetTag("email", identity.Value);
        await using var enumerator = ReadEvents(cancellationToken);

        var entity = UserSettings.Create(identity);

        while (await enumerator.MoveNextAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            entity = enumerator.Current.Apply(entity);
        }
        return entity;
    }

    public async ValueTask<UserInfo> ReadInfo(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("HouseholdStore.Read");

        // TODO: precheck to save resources?
        var identity = _identityProvider.GetIdentity();
        activity?.SetTag("email", identity.Value);
        await using var enumerator = ReadEvents(cancellationToken);

        var entity = UserInfo.Create(identity);

        while (await enumerator.MoveNextAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            entity = enumerator.Current.Apply(entity);
        }
        return entity;
    }

    private async IAsyncEnumerator<UserEvent> ReadEvents(CancellationToken cancellationToken)
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
        var identity = _identityProvider.GetIdentity();
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

    private readonly NpgsqlConnection _connection;
    private readonly IIdentityProvider _identityProvider;
    private readonly TimeProvider _timeProvider;
}
