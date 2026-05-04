namespace Chorbar.Model;

public interface IMailSender
{
    public ValueTask SendAuthToken(Email email, int code, CancellationToken cancellation);
}

public class LogMailer(ILogger logger) : IMailSender
{
    public ValueTask SendAuthToken(Email email, int code, CancellationToken cancellationToken)
    {
        logger.ForContext("code", code).Warning("Should have sent {code} via email", code);

        return ValueTask.CompletedTask;
    }
}
