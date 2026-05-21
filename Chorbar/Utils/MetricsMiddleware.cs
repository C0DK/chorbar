using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Prometheus;

namespace Chorbar.Utils;

public class MetricsMiddleware(RequestDelegate next, IMemoryCache cache)
{
    private static readonly Counter PageViews = Metrics.CreateCounter(
        "chorbar_page_views_total",
        "Total page views per route.",
        new CounterConfiguration
        {
            LabelNames = ["route", "method", "status"],
        }
    );

    private static readonly Counter UniqueVisitors = Metrics.CreateCounter(
        "chorbar_unique_visitors_total",
        "Unique visitors per route (deduped by ip+user-agent over a 24h window).",
        new CounterConfiguration { LabelNames = ["route"] }
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
        var method = context.Request.Method;
        var status = context.Response.StatusCode.ToString(
            System.Globalization.CultureInfo.InvariantCulture
        );

        PageViews.WithLabels(route, method, status).Inc();

        var fingerprint = Fingerprint(context);
        var cacheKey = $"visitor:{route}:{fingerprint}";
        if (!cache.TryGetValue(cacheKey, out _))
        {
            cache.Set(
                cacheKey,
                true,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = VisitorWindow,
                }
            );
            UniqueVisitors.WithLabels(route).Inc();
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

        var method = context.Request.Method;
        return method is "GET" or "POST" or "PUT" or "DELETE" or "PATCH";
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

    private static string Fingerprint(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "anon";
        var ua = context.Request.Headers.UserAgent.ToString();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{ip}|{ua}"));
        return Convert.ToHexString(bytes);
    }

    public static void Use(IApplicationBuilder builder) =>
        builder.UseMiddleware<MetricsMiddleware>();
}
