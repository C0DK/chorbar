using Chorbar.Model;
using Npgsql;
using NpgsqlTypes;

namespace Chorbar;

public class UserStore
{
    public UserStore(NpgsqlConnection connection)
    {
        _connection = connection;
        _timeProvider = TimeProvider.System;
    }

    public UserStore(NpgsqlConnection connection, TimeProvider timeProvider)
    {
        _connection = connection;
        _timeProvider = timeProvider;
    }

    private readonly NpgsqlConnection _connection;
    private readonly TimeProvider _timeProvider;

    public ValueTask<User> Write(
        Email identity,
        UserEventPayload payload,
        CancellationToken cancellationToken
    ) => Write(identity, [payload], cancellationToken);

    public async ValueTask<User> Write(
        Email identity,
        IEnumerable<UserEventPayload> payloads,
        CancellationToken cancellationToken
    )
    {
        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken);
        foreach (var payload in payloads)
        {
            var user = await Read(identity, cancellationToken);
            if (!payload.IsValid(user))
                throw new InvalidOperationException("Event not valid!");
            await using (
                var command = new NpgsqlCommand(
                    @"
INSERT INTO 
  user_event(identity, version, timestamp, payload)
  VALUES($1, $2, $3, $4)
",
                    _connection,
                    transaction
                )
            )
            {
                command.Parameters.AddWithValue(identity.Value);
                command.Parameters.AddWithValue((user.History.LastOrDefault()?.Version ?? 0) + 1);
                command.Parameters.AddWithValue(
                    NpgsqlDbType.TimestampTz,
                    _timeProvider.GetUtcNow()
                );
                command.Parameters.AddWithValue(NpgsqlDbType.Jsonb, payload.Serialize());

                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        var updatedUser = await Read(identity, cancellationToken);

        await transaction.CommitAsync();

        return updatedUser;
    }

    public async ValueTask<User> Read(Email identity, CancellationToken cancellationToken)
    {
        using var command = new NpgsqlCommand(
            @"
SELECT 
  identity, 
  version,
  timestamp,
  payload
FROM user_event
WHERE identity = $1
ORDER BY timestamp
",
            _connection
        );
        command.Parameters.AddWithValue(identity.Value);
        await using var enumerator = command
            .ReadAllAsync(
                reader => new UserEvent(
                    Identity: new Email(reader.GetFieldValue<string>(0)),
                    Version: reader.GetFieldValue<int>(1),
                    Timestamp: reader.GetFieldValue<DateTimeOffset>(2),
                    Payload: UserEventPayload.Deserialize(reader.GetFieldValue<string>(3))
                ),
                cancellationToken
            )
            .GetAsyncEnumerator();
        var user = new User(Email: identity, Chores: [], History: []);

        while (await enumerator.MoveNextAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            user = enumerator.Current.Apply(user);
        }

        return user;
    }
}
