using Chorbar.Model;
using Chorbar.Templates;

namespace Chorbar;

public class BrevoClient(HttpClient client, string apiKey, ILogger logger) : IMailSender
{
    public ValueTask SendAuthToken(Email email, int code, CancellationToken cancellationToken)
    {
        return Send(
            email,
            "Your Chor.bar login code",
            new AuthEmail(code.ToString("D6")),
            cancellationToken
        );
    }

    public async ValueTask Send(
        Email email,
        string subject,
        string htmlContent,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.brevo.com/v3/smtp/email"
        )
        {
            Content = JsonContent.Create(
                new HtmlPayload(
                    _sender,
                    To: [new Identity(email, Name: null)],
                    subject,
                    htmlContent
                )
            ),
        };

        request.Headers.Add("api-key", apiKey);
        var response = await client.SendAsync(request, cancellationToken);

        if ((int)response.StatusCode >= 400)
        {
            logger
                .ForContext("target", request.RequestUri)
                .ForContext("statusCode", response.StatusCode)
                .ForContext("Response.Content", response.Content.ToString())
                .ForContext("message.subject", subject)
                .Error("brevo request failed!");
        }
        response.EnsureSuccessStatusCode();
    }

    private static readonly Identity _sender = new("no-reply@chor.bar", "Chor.bar");

    private sealed record Identity(string Email, string? Name);

    private sealed record HtmlPayload(
        Identity Sender,
        Identity[] To,
        string Subject,
        string HtmlContent
    );
}
