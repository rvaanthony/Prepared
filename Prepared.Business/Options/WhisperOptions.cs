using System.ComponentModel.DataAnnotations;

namespace Prepared.Business.Options;

/// <summary>
/// Configuration options for the Whisper transcription service.
/// Validates configuration at startup to ensure all required values are present.
/// </summary>
public class WhisperOptions
{
    public const string SectionName = "Whisper";

    /// <summary>
    /// OpenAI API key for Whisper transcription. Required.
    /// </summary>
    [Required(ErrorMessage = "Whisper API key is required")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The Whisper model to use (e.g., whisper-1, gpt-4o-mini-transcribe).
    /// </summary>
    [Required(ErrorMessage = "Whisper model is required")]
    public string Model { get; set; } = "whisper-1";

    /// <summary>
    /// The OpenAI endpoint for audio transcription.
    /// </summary>
    [Required(ErrorMessage = "Whisper endpoint is required")]
    [Url(ErrorMessage = "Whisper endpoint must be a valid URL")]
    public string Endpoint { get; set; } = "https://api.openai.com/v1/audio/transcriptions";

    /// <summary>
    /// Optional temperature parameter for Whisper (controls randomness).
    /// Range: 0.0 (deterministic) to 1.0 (more random).
    /// </summary>
    [Range(0.0, 1.0, ErrorMessage = "Temperature must be between 0.0 and 1.0")]
    public double Temperature { get; set; } = 0.0;

    /// <summary>
    /// HTTP client timeout in seconds. Defaults to 60 seconds (longer for audio processing).
    /// </summary>
    [Range(1, 300, ErrorMessage = "Timeout must be between 1 and 300 seconds")]
    public int TimeoutSeconds { get; set; } = 60;
}

