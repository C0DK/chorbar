using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Prometheus;

namespace Chorbar.Utils;

public class MetricsMiddleware(RequestDelegate next, IMemoryCache cache)
{
    private static readonly Counter PageViews = Metrics.CreateCounter(
        "chorbar_page_views_total",
        "Total page views per route (GET only).",
        new CounterConfiguration { LabelNames = ["route", "status", "client", "device"] }
    );

    private static readonly Counter UniqueVisitors = Metrics.CreateCounter(
        "chorbar_unique_visitors_total",
        "Unique visitors per route (deduped by ip+user-agent over a 24h window).",
        new CounterConfiguration { LabelNames = ["route", "client", "device"] }
    );

    // Visitor fingerprints expire after this long, after which a returning
    // ip+ua combo is counted as unique again. Tune to taste.
    private static readonly TimeSpan VisitorWindow = TimeSpan.FromHours(24);

    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        if (!ShouldCount(context))
            return;

        var route = RouteLabel(context);
        var status = context.Response.StatusCode.ToString(
            System.Globalization.CultureInfo.InvariantCulture
        );
        var ua = context.Request.Headers.UserAgent.ToString();
        var client = ClientBucket(ua);
        var device = DeviceBucket(ua);

        PageViews.WithLabels(route, status, client, device).Inc();

        var fingerprint = Fingerprint(context, ua);
        var cacheKey = $"visitor:{route}:{fingerprint}";
        if (!cache.TryGetValue(cacheKey, out _))
        {
            cache.Set(
                cacheKey,
                true,
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = VisitorWindow }
            );
            UniqueVisitors.WithLabels(route, client, device).Inc();
        }
    }

    private static bool ShouldCount(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        if (path.Equals("/metrics", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.StartsWith("/img/", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
            return false;

        return context.Request.Method == "GET";
    }

    // Use the route pattern (e.g. /household/{householdId:int}/edit) rather
    // than the actual path. Otherwise every household id becomes its own
    // label value and prometheus cardinality blows up.
    private static string RouteLabel(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is RouteEndpoint routeEndpoint)
        {
            var pattern = routeEndpoint.RoutePattern.RawText;
            if (!string.IsNullOrEmpty(pattern))
                return pattern.StartsWith('/') ? pattern : "/" + pattern;
        }
        return "unmatched";
    }

    // Coarse browser family. Picked from substrings in order so e.g. Edge
    // (which also says "Chrome" in its UA) gets matched first. The total
    // label-value count is bounded by the number of branches here, which
    // keeps cardinality flat instead of one-series-per-UA-string.
    private static string ClientBucket(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return "unknown";
        if (userAgent.Contains("bot", StringComparison.OrdinalIgnoreCase))
            return "bot";
        if (userAgent.Contains("curl", StringComparison.OrdinalIgnoreCase))
            return "curl";
        if (userAgent.Contains("Edg/", StringComparison.Ordinal))
            return "edge";
        if (userAgent.Contains("OPR/", StringComparison.Ordinal))
            return "opera";
        if (userAgent.Contains("Firefox/", StringComparison.Ordinal))
            return "firefox";
        if (userAgent.Contains("Chrome/", StringComparison.Ordinal))
            return "chrome";
        if (userAgent.Contains("Safari/", StringComparison.Ordinal))
            return "safari";
        return "other";
    }

    // OS / device-platform bucket. Android is checked before Linux because
    // Android UAs include "Linux" in the platform token. iPad/iPhone are
    // checked before Mac for the same reason on modern iPadOS Safari.
    private static string DeviceBucket(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return "unknown";
        if (userAgent.Contains("bot", StringComparison.OrdinalIgnoreCase))
            return "bot";
        if (userAgent.Contains("Android", StringComparison.Ordinal))
            return "android";
        if (
            userAgent.Contains("iPhone", StringComparison.Ordinal)
            || userAgent.Contains("iPad", StringComparison.Ordinal)
            || userAgent.Contains("iPod", StringComparison.Ordinal)
        )
            return "ios";
        if (userAgent.Contains("Windows", StringComparison.Ordinal))
            return "windows";
        if (
            userAgent.Contains("Mac OS X", StringComparison.Ordinal)
            || userAgent.Contains("Macintosh", StringComparison.Ordinal)
        )
            return "mac";
        if (userAgent.Contains("Linux", StringComparison.Ordinal))
            return "linux";
        return "other";
    }

    private static string Fingerprint(HttpContext context, string userAgent)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "anon";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{ip}|{userAgent}"));
        return Convert.ToHexString(bytes);
    }

    public static void Use(IApplicationBuilder builder) =>
        builder.UseMiddleware<MetricsMiddleware>();
}
