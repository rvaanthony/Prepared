namespace Prepared.Business.Interfaces;

/// <summary>
/// Service interface for handling Twilio Media Streams
/// </summary>
public interface IMediaStreamService
{
    /// <summary>
    /// Handles the start of a media stream
    /// </summary>
    /// <param name="streamSid">The stream SID</param>
    /// <param name="callSid">The associated call SID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task HandleStreamStartAsync(string streamSid, string callSid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes incoming media data from the stream
    /// </summary>
    /// <param name="streamSid">The stream SID</param>
    /// <param name="mediaPayload">The base64 encoded media payload</param>
    /// <param name="eventType">The event type (media, start, stop)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ProcessMediaDataAsync(string streamSid, string? mediaPayload, string eventType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles the stop of a media stream
    /// </summary>
    /// <param name="streamSid">The stream SID</param>
    /// <param name="callSid">The associated call SID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task HandleStreamStopAsync(string streamSid, string callSid, CancellationToken cancellationToken = default);
}

