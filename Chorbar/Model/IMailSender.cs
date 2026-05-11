namespace Chorbar.Model;

public interface IMailSender
{
    public ValueTask SendAuthToken(Email email, int code, CancellationToken cancellationToken);
}

public class LogMailer(ILogger logger) : IMailSender
{
    public ValueTask SendAuthToken(Email email, int code, CancellationToken cancellationToken)
    {
        logger.ForContext("code", code).ForContext("email", email.Value).Warning("Sent auth code");

        return ValueTask.CompletedTask;
    }
}
