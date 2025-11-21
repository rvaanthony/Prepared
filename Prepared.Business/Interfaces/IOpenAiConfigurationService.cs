namespace Prepared.Business.Interfaces;

/// <summary>
/// Configuration service for OpenAI settings.
/// Provides read-only access to OpenAI configuration values with defaults.
/// </summary>
public interface IOpenAiConfigurationService
{
    /// <summary>
    /// OpenAI API key for authentication.
    /// </summary>
    string ApiKey { get; }

    /// <summary>
    /// Base endpoint URL for OpenAI API.
    /// </summary>
    string Endpoint { get; }

    /// <summary>
    /// Default model to use when specific model is not specified.
    /// </summary>
    string DefaultModel { get; }

    /// <summary>
    /// Model to use for summarization tasks. Falls back to DefaultModel if not specified.
    /// </summary>
    string SummarizationModel { get; }

    /// <summary>
    /// Model to use for location extraction tasks. Falls back to DefaultModel if not specified.
    /// </summary>
    string LocationModel { get; }

    /// <summary>
    /// HTTP client timeout in seconds.
    /// </summary>
    int TimeoutSeconds { get; }
}

