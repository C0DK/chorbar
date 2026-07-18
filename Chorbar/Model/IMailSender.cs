namespace Chorbar.Model;

public interface IMailSender
{
    public ValueTask SendAuthToken(Email email, int code, CancellationToken cancellationToken);

    public ValueTask SendHouseholdInvite(
        Email email,
        string householdName,
        string householdUrl,
        string inviterName,
        CancellationToken cancellationToken
    );
}

public class LogMailer(ILogger logger) : IMailSender
{
    public ValueTask SendAuthToken(Email email, int code, CancellationToken cancellationToken)
    {
        logger.ForContext("code", code).ForContext("email", email.Value).Warning("Sent auth code");

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
        logger
            .ForContext("email", email.Value)
            .ForContext("householdName", householdName)
            .ForContext("householdUrl", householdUrl)
            .ForContext("inviterName", inviterName)
            .Warning("Sent household invite");

        return ValueTask.CompletedTask;
    }
}
