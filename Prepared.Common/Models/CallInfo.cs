using System.ComponentModel.DataAnnotations;

namespace Prepared.Common.Models;

/// <summary>
/// Represents information about an incoming Twilio call
/// </summary>
public class CallInfo
{
    /// <summary>
    /// Unique identifier for the call (Twilio CallSid)
    /// </summary>
    [Required]
    public string CallSid { get; set; } = string.Empty;

    /// <summary>
    /// The phone number that initiated the call
    /// </summary>
    [Required]
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// The phone number that received the call
    /// </summary>
    [Required]
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// The status of the call
    /// </summary>
    public Enums.CallStatus Status { get; set; }

    /// <summary>
    /// The direction of the call (inbound/outbound)
    /// </summary>
    public string Direction { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the call was initiated
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Timestamp when the call was completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Duration of the call in seconds
    /// </summary>
    public int? Duration { get; set; }

    /// <summary>
    /// Whether the call has an active media stream
    /// </summary>
    public bool HasActiveStream { get; set; }

    /// <summary>
    /// The account SID associated with the call
    /// </summary>
    public string? AccountSid { get; set; }
}

