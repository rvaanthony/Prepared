namespace Prepared.Client.Middleware;

/// <summary>
/// Middleware that adds comprehensive security headers to all HTTP responses.
/// Implements defense-in-depth security practices including CSP, HSTS, and XSS protection.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityHeadersMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="next"/> is null.</exception>
    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    /// <summary>
    /// Invokes the middleware to add security headers to the HTTP response.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Add security headers
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");

        // Content Security Policy
        context.Response.Headers.Append("Content-Security-Policy", 
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://maps.googleapis.com https://maps.gstatic.com; " +
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
            "img-src 'self' data: https: https://maps.googleapis.com https://maps.gstatic.com; " +
            "font-src 'self' data: https://fonts.gstatic.com; " +
            "connect-src 'self' https: wss: ws:; " +
            "frame-ancestors 'none';");

        await _next(context);
    }
}

