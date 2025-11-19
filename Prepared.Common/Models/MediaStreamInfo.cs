namespace Prepared.Common.Models;

/// <summary>
/// Represents information about a Twilio Media Stream
/// </summary>
public class MediaStreamInfo
{
    /// <summary>
    /// The unique identifier for the media stream
    /// </summary>
    public string StreamSid { get; set; } = string.Empty;

    /// <summary>
    /// The associated call SID
    /// </summary>
    public string CallSid { get; set; } = string.Empty;

    /// <summary>
    /// The status of the stream (started, stopped)
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the stream started
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Timestamp when the stream stopped
    /// </summary>
    public DateTime? StoppedAt { get; set; }
}

