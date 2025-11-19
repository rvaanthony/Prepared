using System.ComponentModel.DataAnnotations;

namespace Prepared.Client.Options;

/// <summary>
/// Configuration options for rate limiting middleware.
/// Allows fine-grained control over rate limiting behavior.
/// </summary>
public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Maximum number of requests allowed per time window.
    /// </summary>
    [Range(1, 10000, ErrorMessage = "MaxRequests must be between 1 and 10000")]
    public int MaxRequests { get; set; } = 100;

    /// <summary>
    /// Time window in seconds for rate limiting.
    /// </summary>
    [Range(1, 3600, ErrorMessage = "TimeWindowSeconds must be between 1 and 3600 seconds")]
    public int TimeWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to enable rate limiting. Useful for development/testing.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

