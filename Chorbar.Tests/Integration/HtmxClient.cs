using System.Net;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Chorbar.Tests.Integration;

public class HtmxClient
{
    private readonly HttpClient _client;
    private string? _csrfToken;

    public HtmxClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<IDocument> GetHtmlDoc(string url, CancellationToken ct = default)
    {
        using var response = await _client.GetAsync(url, ct);
        var doc = await ParseHtmlAsync(response, ct);
        _csrfToken ??= TryExtractCsrfToken(doc);
        return doc;
    }

    public async Task<HttpResponseMessage> PostForm(
        string url,
        Dictionary<string, string> form,
        CancellationToken ct = default
    )
    {
        Assert.That(_csrfToken, Is.Not.Null, "CSRF token must be extracted before POSTing");

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.Add("HX-Request", "true");
        request.Headers.Add("X-CSRF-TOKEN", _csrfToken);
        return await _client.SendAsync(request, ct);
    }

    public async Task<IDocument> PostFormAndParse(
        string url,
        Dictionary<string, string> form,
        CancellationToken ct = default
    )
    {
        using var response = await PostForm(url, form, ct);
        return await ParseHtmlAsync(response, ct);
    }

    public Task<IDocument> PostFormAndParse(
        string url,
        (string key, string value)[] form,
        CancellationToken ct = default
    ) => PostFormAndParse(url, form.ToDictionary(f => f.key, f => f.value), ct);

    public Task<HttpResponseMessage> PostForm(
        string url,
        (string key, string value)[] form,
        CancellationToken ct = default
    ) => PostForm(url, form.ToDictionary(f => f.key, f => f.value), ct);

    public static async Task<IDocument> ParseHtmlAsync(
        HttpResponseMessage response,
        CancellationToken ct = default
    )
    {
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(ct);
        return new HtmlParser().ParseDocument(html);
    }

    private static string? TryExtractCsrfToken(IDocument doc)
    {
        var body = doc.QuerySelector("body");
        if (body is null)
            return null;

        var hxHeaders = body.GetAttribute("hx-headers");
        if (string.IsNullOrEmpty(hxHeaders))
            return null;

        try
        {
            using var json = JsonDocument.Parse(hxHeaders);
            return json.RootElement.GetProperty("X-CSRF-TOKEN").GetString();
        }
        catch
        {
            return null;
        }
    }
}
