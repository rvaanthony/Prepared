using System.ComponentModel.DataAnnotations;

namespace Prepared.Business.Options;

/// <summary>
/// Shared OpenAI configuration for summarization/location services.
/// Validates configuration at startup to ensure all required values are present.
/// </summary>
public class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    /// <summary>
    /// OpenAI API key. Required for all OpenAI operations.
    /// </summary>
    [Required(ErrorMessage = "OpenAI API key is required")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Base endpoint for OpenAI API. Defaults to official OpenAI endpoint.
    /// </summary>
    [Required(ErrorMessage = "OpenAI endpoint is required")]
    [Url(ErrorMessage = "OpenAI endpoint must be a valid URL")]
    public string Endpoint { get; set; } = "https://api.openai.com/v1/";

    /// <summary>
    /// Default model to use when specific model is not specified.
    /// </summary>
    [Required(ErrorMessage = "Default model is required")]
    public string DefaultModel { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Model to use for summarization tasks.
    /// Falls back to DefaultModel if not specified.
    /// </summary>
    public string? SummarizationModel { get; set; }

    /// <summary>
    /// Model to use for location extraction tasks.
    /// Falls back to DefaultModel if not specified.
    /// </summary>
    public string? LocationModel { get; set; }

    /// <summary>
    /// HTTP client timeout in seconds. Defaults to 60 seconds for gpt-5-mini compatibility.
    /// Note: The default resilience handler has a 10s attempt timeout, but the HTTP client timeout
    /// will be the limiting factor for longer-running requests.
    /// </summary>
    [Range(1, 300, ErrorMessage = "Timeout must be between 1 and 300 seconds")]
    public int TimeoutSeconds { get; set; } = 60;
}

