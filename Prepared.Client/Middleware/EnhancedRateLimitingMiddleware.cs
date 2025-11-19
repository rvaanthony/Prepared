using System.Collections.Concurrent;

namespace Prepared.Client.Middleware;

public class EnhancedRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ConcurrentDictionary<string, RateLimitInfo> _rateLimitCache = new();
    private readonly int _maxRequests = 100;
    private readonly TimeSpan _timeWindow = TimeSpan.FromMinutes(1);

    public EnhancedRateLimitingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var now = DateTime.UtcNow;

        // Clean up old entries
        CleanupOldEntries(now);

        // Check rate limit
        var rateLimitInfo = _rateLimitCache.GetOrAdd(clientIp, _ => new RateLimitInfo { FirstRequest = now });

        if (rateLimitInfo.RequestCount >= _maxRequests)
        {
            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.Headers.Append("Retry-After", ((int)_timeWindow.TotalSeconds).ToString());
            await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
            return;
        }

        rateLimitInfo.RequestCount++;
        rateLimitInfo.LastRequest = now;

        await _next(context);
    }

    private void CleanupOldEntries(DateTime now)
    {
        var keysToRemove = _rateLimitCache
            .Where(kvp => now - kvp.Value.LastRequest > _timeWindow)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _rateLimitCache.TryRemove(key, out _);
        }
    }

    private class RateLimitInfo
    {
        public int RequestCount { get; set; }
        public DateTime FirstRequest { get; set; }
        public DateTime LastRequest { get; set; }
    }
}

