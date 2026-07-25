using Chorbar.Model;

namespace Chorbar.Tests.Integration;

public class FakeMailer : IMailSender
{
    public Email? LastEmail { get; private set; }
    public int? LastCode { get; private set; }
    public string? LastLoginUrl { get; private set; }

    public Email? LastInviteEmail { get; private set; }
    public string? LastHouseholdName { get; private set; }
    public string? LastHouseholdUrl { get; private set; }
    public string? LastInviterName { get; private set; }

    public ValueTask SendAuthToken(
        Email email,
        int code,
        string loginUrl,
        CancellationToken cancellationToken
    )
    {
        LastEmail = email;
        LastCode = code;
        LastLoginUrl = loginUrl;
        return ValueTask.CompletedTask;
    }

    public ValueTask SendHouseholdInvite(
        Email email,
        string householdName,
        string householdUrl,
        string inviterName,
        CancellationToken cancellationToken
    )
    {
        LastInviteEmail = email;
        LastHouseholdName = householdName;
        LastHouseholdUrl = householdUrl;
        LastInviterName = inviterName;
        return ValueTask.CompletedTask;
    }

    public void Reset()
    {
        LastEmail = null;
        LastCode = null;
        LastLoginUrl = null;
        LastInviteEmail = null;
        LastHouseholdName = null;
        LastHouseholdUrl = null;
        LastInviterName = null;
    }
}
