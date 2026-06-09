using Chorbar.Model;

namespace Chorbar.Tests.Integration;

public class FakeMailer : IMailSender
{
    public Email? LastEmail { get; private set; }
    public int? LastCode { get; private set; }

    public ValueTask SendAuthToken(Email email, int code, CancellationToken cancellationToken)
    {
        LastEmail = email;
        LastCode = code;
        return ValueTask.CompletedTask;
    }

    public void Reset()
    {
        LastEmail = null;
        LastCode = null;
    }
}
