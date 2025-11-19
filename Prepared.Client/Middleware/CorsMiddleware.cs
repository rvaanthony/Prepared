using Microsoft.Extensions.Options;
using Prepared.Client.Options;

namespace Prepared.Client.Middleware;

/// <summary>
/// Secure CORS middleware with whitelist-based origin validation.
/// Prevents unauthorized cross-origin requests by validating against configured allowed origins.
/// </summary>
public class CorsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly CorsOptions _options;
    private readonly ILogger<CorsMiddleware> _logger;
    private readonly HashSet<string> _allowedOrigins;

    public CorsMiddleware(
        RequestDelegate next,
        IOptions<CorsOptions> options,
        ILogger<CorsMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Build hash set for O(1) lookup performance
        _allowedOrigins = new HashSet<string>(
            _options.AllowedOrigins ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var origin = context.Request.Headers["Origin"].ToString();

        if (context.Request.Method == "OPTIONS")
        {
            await HandlePreflightRequestAsync(context, origin);
            return;
        }

        if (!string.IsNullOrEmpty(origin) && IsOriginAllowed(origin))
        {
            context.Response.Headers.Append("Access-Control-Allow-Origin", origin);
            
            if (_options.AllowCredentials)
            {
                context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
            }

            context.Response.Headers.Append("Access-Control-Expose-Headers", 
                string.Join(", ", _options.AllowedHeaders));
        }
        else if (!string.IsNullOrEmpty(origin))
        {
            _logger.LogWarning("Blocked CORS request from unauthorized origin: {Origin}", origin);
        }

        await _next(context);
    }

    private async Task HandlePreflightRequestAsync(HttpContext context, string origin)
    {
        if (string.IsNullOrEmpty(origin) || !IsOriginAllowed(origin))
        {
            _logger.LogWarning("Blocked CORS preflight from unauthorized origin: {Origin}", origin);
            context.Response.StatusCode = 403; // Forbidden
            return;
        }

        context.Response.Headers.Append("Access-Control-Allow-Origin", origin);
        
        if (_options.AllowCredentials)
        {
            context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
        }

        context.Response.Headers.Append("Access-Control-Allow-Methods", 
            string.Join(", ", _options.AllowedMethods));
        
        context.Response.Headers.Append("Access-Control-Allow-Headers", 
            string.Join(", ", _options.AllowedHeaders));
        
        context.Response.Headers.Append("Access-Control-Max-Age", 
            _options.MaxAgeSeconds.ToString());

        context.Response.StatusCode = 204; // No Content
        await Task.CompletedTask;
    }

    private bool IsOriginAllowed(string origin)
    {
        if (_allowedOrigins.Count == 0)
        {
            return false; // CORS disabled when no origins configured
        }

        return _allowedOrigins.Contains(origin);
    }
}

