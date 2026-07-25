using Chorbar.Model;
using Chorbar.Utils;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Chorbar.Tests.Integration;

[TestFixture]
public class HouseholdInviteTest
{
    private NpgsqlConnection _conn = null!;
    private FakeMailer _fakeMailer = null!;
    private HouseholdStore _store = null!;
    private UserReader _userReader = null!;
    private FakeTimeProvider _timeProvider = null!;

    [SetUp]
    public async Task SetUp()
    {
        _conn = await DatabaseFixture.DataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("TRUNCATE household_event", _conn);
        await cmd.ExecuteNonQueryAsync();

        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        _fakeMailer = new FakeMailer();
        _store = new HouseholdStore(
            _conn,
            new StaticIdentityProvider(_userA),
            _timeProvider,
            new EventMetrics()
        );
        _userReader = new UserReader(_conn, new Chorbar.Utils.AsyncLazyCache<Email, UserInfo>());
    }

    [TearDown]
    public async Task TearDown() => await _conn.DisposeAsync();

    [Test, CancelAfter(10_000)]
    public async Task Invite_SendsInvitationEmail(CancellationToken cancellationToken)
    {
        var householdId = await _store.Create("Test Household", cancellationToken);
        await _store.Write(householdId, new AddMember(_userA), cancellationToken);

        var household = await _store.Read(householdId, cancellationToken);
        var inviterName = await _userReader.DisplayName(_userA, cancellationToken);
        var householdUrl = $"http://localhost/household/{householdId.Value}/";

        await _fakeMailer.SendHouseholdInvite(
            _userB,
            household.Name,
            householdUrl,
            inviterName,
            cancellationToken
        );

        Assert.That(_fakeMailer.LastInviteEmail, Is.EqualTo(_userB));
        Assert.That(_fakeMailer.LastHouseholdName, Is.EqualTo("Test Household"));
        Assert.That(_fakeMailer.LastInviterName, Is.EqualTo(inviterName));
        Assert.That(_fakeMailer.LastHouseholdUrl, Does.Contain($"/household/{householdId.Value}/"));
    }

    private static Email _userA => new Email("alice@example.com");
    private static Email _userB => new Email("bob@example.com");
}
