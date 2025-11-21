namespace Prepared.Business.Interfaces;

/// <summary>
/// Configuration service for Whisper transcription settings.
/// Provides read-only access to Whisper configuration values with defaults.
/// </summary>
public interface IWhisperConfigurationService
{
    /// <summary>
    /// OpenAI API key for Whisper transcription.
    /// </summary>
    string ApiKey { get; }

    /// <summary>
    /// The Whisper model to use (e.g., whisper-1).
    /// </summary>
    string Model { get; }

    /// <summary>
    /// The OpenAI endpoint for audio transcription.
    /// </summary>
    string Endpoint { get; }

    /// <summary>
    /// Temperature parameter for Whisper (controls randomness). Range: 0.0 to 1.0.
    /// </summary>
    double Temperature { get; }

    /// <summary>
    /// HTTP client timeout in seconds.
    /// </summary>
    int TimeoutSeconds { get; }
}

