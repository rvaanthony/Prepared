namespace Prepared.Common.Models;

/// <summary>
/// Represents a summarized view of a call transcript.
/// </summary>
public class TranscriptSummary
{
    public string CallSid { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<string> KeyFindings { get; init; } = Array.Empty<string>();
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
}

