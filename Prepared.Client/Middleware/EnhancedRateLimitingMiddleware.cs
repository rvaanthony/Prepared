using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Options;
using Prepared.Client.Options;

namespace Prepared.Client.Middleware;

/// <summary>
/// Enhanced rate limiting middleware with configurable limits and efficient cleanup.
/// Uses sliding window algorithm for accurate rate limiting.
/// </summary>
public class EnhancedRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RateLimitingOptions _options;
    private readonly ILogger<EnhancedRateLimitingMiddleware> _logger;
    private readonly ConcurrentDictionary<string, RateLimitInfo> _rateLimitCache = new();
    private DateTime _lastCleanup = DateTime.UtcNow;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

    public EnhancedRateLimitingMiddleware(
        RequestDelegate next,
        IOptions<RateLimitingOptions> options,
        ILogger<EnhancedRateLimitingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var now = DateTime.UtcNow;
        var timeWindow = TimeSpan.FromSeconds(_options.TimeWindowSeconds);

        // Periodic cleanup to prevent memory leaks (only every 5 minutes)
        if (now - _lastCleanup > _cleanupInterval)
        {
            CleanupOldEntries(now, timeWindow);
            _lastCleanup = now;
        }

        // Check rate limit with sliding window
        var rateLimitInfo = _rateLimitCache.AddOrUpdate(
            clientIp,
            _ => new RateLimitInfo(now),
            (_, existing) =>
            {
                if (now - existing.FirstRequest > timeWindow)
                {
                    existing.Reset(now);
                }
                else
                {
                    existing.Touch(now);
                }

                return existing;
            });

        if (rateLimitInfo.RequestCount > _options.MaxRequests)
        {
            _logger.LogWarning(
                "Rate limit exceeded for IP {ClientIp}: {RequestCount}/{MaxRequests} in {TimeWindow}s",
                clientIp, rateLimitInfo.RequestCount, _options.MaxRequests, _options.TimeWindowSeconds);

            context.Response.StatusCode = 429; // Too Many Requests
            var retryAfterSeconds = Math.Max(
                1,
                _options.TimeWindowSeconds - (int)(now - rateLimitInfo.FirstRequest).TotalSeconds);
            context.Response.Headers.Append("Retry-After", retryAfterSeconds.ToString(CultureInfo.InvariantCulture));
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                $"{{\"error\":\"Rate limit exceeded. Maximum {_options.MaxRequests} requests per {_options.TimeWindowSeconds} seconds.\"}}");
            return;
        }

        await _next(context);
    }

    private void CleanupOldEntries(DateTime now, TimeSpan timeWindow)
    {
        var keysToRemove = new List<string>();
        var cutoff = now - timeWindow;

        foreach (var kvp in _rateLimitCache)
        {
            if (kvp.Value.LastRequest < cutoff)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _rateLimitCache.TryRemove(key, out _);
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired rate limit entries", keysToRemove.Count);
        }
    }

    private sealed class RateLimitInfo
    {
        public RateLimitInfo(DateTime timestamp) => Reset(timestamp);

        public int RequestCount { get; private set; }
        public DateTime FirstRequest { get; private set; }
        public DateTime LastRequest { get; private set; }

        public void Reset(DateTime timestamp)
        {
            RequestCount = 1;
            FirstRequest = timestamp;
            LastRequest = timestamp;
        }

        public void Touch(DateTime timestamp)
        {
            RequestCount++;
            LastRequest = timestamp;
        }
    }
}

