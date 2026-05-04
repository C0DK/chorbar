using Chorbar.Model;

namespace Chorbar;

public class BrevoClient(HttpClient client, string apiKey, ILogger logger) : IMailSender
{
    public ValueTask SendAuthToken(Email email, int code, CancellationToken cancellationToken)
    {
        return Send(
            email,
            "Login code",
            $"""
            Hi!

            You requested a login code to Chor.bar. Please input this number where you requested it:

               {code}

            It's only valid for a few minutes.

            Dont share it with any other person, and don't input it any other place than the official chor.bar page

            Kind regards,
            the Chor.bar team
            """,
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
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
        {
            Content = JsonContent.Create(
                new TextPayload(Sender, To: [new Identity(email, Name: null)], subject, htmlContent)
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

    private static Identity Sender = new("no-reply@chor.bar", "Chor.bar");

    private record Identity(string Email, string? Name);

    private record HtmlPayload(Identity Sender, Identity[] To, string Subject, string HtmlContent);

    private record TextPayload(Identity Sender, Identity[] To, string Subject, string TextContent);
}
