namespace Prepared.Business.Options;

/// <summary>
/// Configuration options for the Whisper transcription service.
/// </summary>
public class WhisperOptions
{
    public const string SectionName = "Whisper";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The Whisper model to use (e.g., whisper-1, gpt-4o-mini-transcribe).
    /// </summary>
    public string Model { get; set; } = "whisper-1";

    /// <summary>
    /// The OpenAI endpoint for audio transcription.
    /// </summary>
    public string Endpoint { get; set; } = "https://api.openai.com/v1/audio/transcriptions";

    /// <summary>
    /// Optional temperature parameter for Whisper (controls randomness).
    /// </summary>
    public double Temperature { get; set; } = 0.0;
}

