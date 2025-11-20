using System.ComponentModel.DataAnnotations;

namespace Prepared.Business.Options;

/// <summary>
/// Configuration options for Twilio Media Stream audio processing.
/// </summary>
public class MediaStreamOptions
{
    public const string SectionName = "MediaStream";

    /// <summary>
    /// Minimum audio buffer size in seconds before sending to transcription service.
    /// At 8kHz μ-law, 1 second = ~8000 bytes.
    /// Recommended: 2-3 seconds for better Whisper transcription accuracy.
    /// </summary>
    [Range(0.5, 10.0, ErrorMessage = "AudioBufferSeconds must be between 0.5 and 10.0")]
    public double AudioBufferSeconds { get; set; } = 2.0;

    /// <summary>
    /// Silence detection threshold (0.0 to 1.0).
    /// If this percentage of samples are silent, the chunk is skipped.
    /// Default: 0.9 (90% silent = skip)
    /// </summary>
    [Range(0.0, 1.0, ErrorMessage = "SilenceThreshold must be between 0.0 and 1.0")]
    public double SilenceThreshold { get; set; } = 0.9;

    /// <summary>
    /// Sample rate for incoming audio (Hz).
    /// Twilio Media Streams use 8000 Hz by default.
    /// </summary>
    [Range(8000, 48000, ErrorMessage = "SampleRate must be between 8000 and 48000")]
    public int SampleRate { get; set; } = 8000;
}

