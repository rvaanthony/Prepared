namespace Prepared.Common.Enums;

/// <summary>
/// Represents the status of a Twilio call
/// </summary>
public enum CallStatus
{
    /// <summary>
    /// Call is queued and waiting to be initiated
    /// </summary>
    Queued = 0,

    /// <summary>
    /// Call is being initiated
    /// </summary>
    Initiated = 1,

    /// <summary>
    /// Call is ringing
    /// </summary>
    Ringing = 2,

    /// <summary>
    /// Call is in progress
    /// </summary>
    InProgress = 3,

    /// <summary>
    /// Call completed successfully
    /// </summary>
    Completed = 4,

    /// <summary>
    /// Call was busy
    /// </summary>
    Busy = 5,

    /// <summary>
    /// Call failed
    /// </summary>
    Failed = 6,

    /// <summary>
    /// Call was not answered
    /// </summary>
    NoAnswer = 7,

    /// <summary>
    /// Call was canceled
    /// </summary>
    Canceled = 8
}

