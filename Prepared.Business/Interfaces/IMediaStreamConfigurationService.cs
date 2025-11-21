namespace Prepared.Business.Interfaces;

/// <summary>
/// Configuration service for Twilio Media Stream settings.
/// Provides read-only access to Media Stream configuration values with defaults.
/// </summary>
public interface IMediaStreamConfigurationService
{
    /// <summary>
    /// Minimum audio buffer size in seconds before sending to transcription service.
    /// </summary>
    double AudioBufferSeconds { get; }

    /// <summary>
    /// Silence detection threshold (0.0 to 1.0).
    /// If this percentage of samples are silent, the chunk is skipped.
    /// </summary>
    double SilenceThreshold { get; }

    /// <summary>
    /// Sample rate for incoming audio (Hz). Twilio Media Streams use 8000 Hz by default.
    /// </summary>
    int SampleRate { get; }
}

