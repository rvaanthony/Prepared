using System.ComponentModel.DataAnnotations;

namespace Prepared.Client.Options;

/// <summary>
/// Configuration options for CORS middleware.
/// Provides secure, whitelist-based CORS configuration.
/// </summary>
public class CorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>
    /// List of allowed origins. Empty list means CORS is disabled.
    /// Example: ["https://example.com", "https://app.example.com"]
    /// </summary>
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether to allow credentials (cookies, authorization headers).
    /// Should be true only when origins are explicitly whitelisted.
    /// </summary>
    public bool AllowCredentials { get; set; } = true;

    /// <summary>
    /// Allowed HTTP methods. Defaults to common REST methods.
    /// </summary>
    public string[] AllowedMethods { get; set; } = { "GET", "POST", "PUT", "DELETE", "OPTIONS" };

    /// <summary>
    /// Allowed HTTP headers.
    /// </summary>
    public string[] AllowedHeaders { get; set; } = { "Content-Type", "Authorization", "X-CSRF-TOKEN" };

    /// <summary>
    /// Maximum age in seconds for preflight requests.
    /// </summary>
    [Range(0, 86400, ErrorMessage = "MaxAgeSeconds must be between 0 and 86400")]
    public int MaxAgeSeconds { get; set; } = 3600;
}

