using System.Diagnostics;
using Chorbar.Model;
using Chorbar.Utils;
using Npgsql;
using NpgsqlTypes;

namespace Chorbar;

public class UserStore
{
    public const string ActivitySourceName = "Chorbar.UserStore";
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private readonly NpgsqlConnection _connection;
    private readonly IIdentityProvider _identityProvider;
    private readonly TimeProvider _timeProvider;
    private readonly UserReader _reader;
    private readonly IAsyncLazyCache<Email, UserInfo> _infoCache;

    public UserStore(
        NpgsqlConnection connection,
        IIdentityProvider identityProvider,
        IAsyncLazyCache<Email, UserInfo> infoCache
    )
    {
        _connection = connection;
        _identityProvider = identityProvider;
        _timeProvider = TimeProvider.System;
        _infoCache = infoCache;
        _reader = new UserReader(connection, infoCache);
    }

    public UserStore(
        NpgsqlConnection connection,
        IIdentityProvider identityProvider,
        TimeProvider timeProvider,
        IAsyncLazyCache<Email, UserInfo> infoCache
    )
    {
        _connection = connection;
        _identityProvider = identityProvider;
        _timeProvider = timeProvider;
        _infoCache = infoCache;
        _reader = new UserReader(connection, infoCache);
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
        var clearCache = false;
        foreach (var payload in payloads)
        {
            await WriteEvent(payload, cancellationToken);
            if (payload is SetDisplayName)
                clearCache = true;
        }
        var updatedEntity = await ReadSettings(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // Eventually this should be cleared via a database trigger / NOTIFY
        // so that other processes also invalidate their caches.
        if (clearCache)
            _infoCache?.Remove(identity);

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

        var identity = _identityProvider.GetIdentity();
        activity?.SetTag("email", identity.Value);
        await using var enumerator = _reader.ReadEvents(identity, cancellationToken);

        var entity = UserSettings.Create(identity);

        while (await enumerator.MoveNextAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            entity = enumerator.Current.Apply(entity);
        }
        return entity;
    }

    public ValueTask<UserInfo> ReadInfo(CancellationToken cancellationToken) =>
        _reader.Info(_identityProvider.GetIdentity(), cancellationToken);
}
