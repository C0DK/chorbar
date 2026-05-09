using System.Runtime.CompilerServices;
using Chorbar.Model;
using Npgsql;
using NpgsqlTypes;

namespace Chorbar;

public class HouseholdStore
{
    public HouseholdStore(NpgsqlConnection connection, IIdentityProvider identityProvider)
    {
        _connection = connection;
        _identityProvider = identityProvider;
        _timeProvider = TimeProvider.System;
    }

    public HouseholdStore(
        NpgsqlConnection connection,
        IIdentityProvider identityProvider,
        TimeProvider timeProvider
    )
    {
        _connection = connection;
        _identityProvider = identityProvider;
        _timeProvider = timeProvider;
    }

    private readonly NpgsqlConnection _connection;
    private readonly IIdentityProvider _identityProvider;
    private readonly TimeProvider _timeProvider;

    public async ValueTask<HouseholdId> New(string name, CancellationToken cancellationToken)
    {
        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken);
        var identity = _identityProvider.GetIdentity();
        HouseholdId id;
        await using (
            var command = new NpgsqlCommand(
                //language=sql
                """
                INSERT INTO
                    household_event(household_id, version, timestamp, created_by, payload)
                    VALUES(nextval('household_id_seq'), 1, $1, $2, $3)
                RETURNING household_id;  
                """,
                _connection,
                transaction
            )
        )
        {
            command.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, _timeProvider.GetUtcNow());
            command.Parameters.AddWithValue(identity.Value);
            command.Parameters.AddWithValue(
                NpgsqlDbType.Jsonb,
                new CreateNewHousehold(name).Serialize()
            );

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException();

            id = new HouseholdId(reader.GetFieldValue<int>(0));
        }
        var updatedEntity = await Read(id, cancellationToken);

        await transaction.CommitAsync();

        return updatedEntity.Id;
    }

    public ValueTask<Household> Write(
        HouseholdId id,
        HouseholdEventPayload payload,
        CancellationToken cancellationToken
    ) => Write(id, [payload], cancellationToken);

    public async ValueTask<Household> Write(
        HouseholdId id,
        IEnumerable<HouseholdEventPayload> payloads,
        CancellationToken cancellationToken
    )
    {
        var identity = _identityProvider.GetIdentity();
        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken);
        foreach (var payload in payloads)
        {
            var entity = await Read(id, cancellationToken);
            if (!entity.Members.Contains(identity))
                throw new NotMemberOfHouseholdException(id, identity);
            if (!payload.IsValid(entity))
                throw new InvalidOperationException("Event not valid!");
            await using (
                var command = new NpgsqlCommand(
                    //language=sql
                    """
                    INSERT INTO
                      household_event(household_id, version, timestamp, created_by, payload)
                    VALUES($1, $2, $3, $4, $5)
                    """,
                    _connection,
                    transaction
                )
            )
            {
                command.Parameters.AddWithValue(id.Value);
                command.Parameters.AddWithValue((entity.History.LastOrDefault()?.Version ?? 0) + 1);
                command.Parameters.AddWithValue(
                    NpgsqlDbType.TimestampTz,
                    _timeProvider.GetUtcNow()
                );
                command.Parameters.AddWithValue(identity.Value);
                command.Parameters.AddWithValue(NpgsqlDbType.Jsonb, payload.Serialize());

                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        var updatedEntity = await Read(id, cancellationToken);

        await transaction.CommitAsync();

        return updatedEntity;
    }

    public async IAsyncEnumerable<Household> List(
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        // todo should also respect future renames etc. :D
        using var command = new NpgsqlCommand(
            //language=sql
            $"""
SELECT
  household_id,
  version,
  timestamp,
  created_by,
  payload
FROM household_event
WHERE household_id IN (
    SELECT DISTINCT household_id
    FROM household_event
    WHERE (payload ->> 'Kind' = '{CreateNewHousehold.Kind}' AND created_by = $1)
       OR (payload ->> 'Kind' = '{AddMember.Kind}' AND payload ->> 'User' = $1)
    )
ORDER BY household_id, timestamp
""",
            _connection
        );
        command.Parameters.AddWithValue(_identityProvider.GetIdentity().Value);
        await using var enumerator = command
            .ReadAllAsync(
                reader => new HouseholdEvent(
                    HouseholdId: new HouseholdId(reader.GetFieldValue<int>(0)),
                    Version: reader.GetFieldValue<int>(1),
                    Timestamp: reader.GetFieldValue<DateTimeOffset>(2),
                    Payload: HouseholdEventPayload.Deserialize(reader.GetFieldValue<string>(4)),
                    CreatedBy: new Email(reader.GetFieldValue<string>(3))
                ),
                cancellationToken
            )
            .GetAsyncEnumerator();
        Household? household = null;
        while (await enumerator.MoveNextAsync())
        {
            if (household?.Id != enumerator.Current.HouseholdId)
            {
                if (
                    household is not null
                    && household.Members.Contains(_identityProvider.GetIdentity())
                )
                    yield return household;

                household = Create(enumerator.Current);
            }
            else
                household = enumerator.Current.Apply(household);
        }
        if (household is not null && household.Members.Contains(_identityProvider.GetIdentity()))
            yield return household;
    }

    public async ValueTask<Household> Read(HouseholdId id, CancellationToken cancellationToken)
    {
        var household = await ReadEvents(id, cancellationToken);

        // TODO: precheck to save resources?
        var identity = _identityProvider.GetIdentity();
        if (!household.Members.Contains(identity))
            throw new NotMemberOfHouseholdException(id, identity);
        return household;
    }

    public async ValueTask<Household?> ReadForIcal(
        HouseholdId id,
        string token,
        CancellationToken cancellationToken
    )
    {
        Household household;
        try
        {
            household = await ReadEvents(id, cancellationToken);
        }
        catch (HouseholdNotFound)
        {
            return null;
        }

        return household.IcalToken == token ? household : null;
    }

    private async ValueTask<Household> ReadEvents(
        HouseholdId id,
        CancellationToken cancellationToken
    )
    {
        using var command = new NpgsqlCommand(
            //language=sql
            """
            SELECT
              household_id,
              version,
              timestamp,
              created_by,
              payload
            FROM household_event
            WHERE household_id = $1
            ORDER BY timestamp
            """,
            _connection
        );
        command.Parameters.AddWithValue(id.Value);
        await using var enumerator = command
            .ReadAllAsync(
                reader => new HouseholdEvent(
                    HouseholdId: new HouseholdId(reader.GetFieldValue<int>(0)),
                    Version: reader.GetFieldValue<int>(1),
                    Timestamp: reader.GetFieldValue<DateTimeOffset>(2),
                    Payload: HouseholdEventPayload.Deserialize(reader.GetFieldValue<string>(4)),
                    CreatedBy: new Email(reader.GetFieldValue<string>(3))
                ),
                cancellationToken
            )
            .GetAsyncEnumerator();

        if (!await enumerator.MoveNextAsync())
            throw new HouseholdNotFound(id);

        var household = Create(enumerator.Current);

        while (await enumerator.MoveNextAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            household = enumerator.Current.Apply(household);
        }

        return household;
    }

    private static Household Create(HouseholdEvent genesisEvent)
    {
        if (genesisEvent.Payload is not CreateNewHousehold genesisEventPayload)
            throw new InvalidOperationException();

        return new Household(
            Id: genesisEvent.HouseholdId,
            Name: genesisEventPayload.Name.Trim(),
            Creator: genesisEvent.CreatedBy,
            Members: [genesisEvent.CreatedBy],
            Chores: [],
            ShoppingListItems: [],
            History: [genesisEvent]
        );
    }
}

public class HouseholdNotFound(HouseholdId id)
    : Exception($"Household with id #{id} could not be found");
