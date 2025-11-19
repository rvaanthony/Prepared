namespace Prepared.Common.Models;

/// <summary>
/// Represents a transcription result produced by the transcription service.
/// </summary>
public class TranscriptionResult
{
    public string CallSid { get; init; } = string.Empty;
    public string StreamSid { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public bool IsFinal { get; init; }
    public double? Confidence { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}

