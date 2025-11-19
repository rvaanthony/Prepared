namespace Prepared.Business.Interfaces;

/// <summary>
/// Interface for broadcasting transcript updates via SignalR
/// This abstraction allows the business layer to send updates without directly depending on SignalR
/// </summary>
public interface ITranscriptHub
{
    /// <summary>
    /// Broadcasts a transcript update to all clients connected to a specific call
    /// </summary>
    /// <param name="callSid">The call identifier</param>
    /// <param name="transcript">The transcript text</param>
    /// <param name="isFinal">Whether this is a final transcript or an interim update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task BroadcastTranscriptUpdateAsync(
        string callSid,
        string transcript,
        bool isFinal = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a call status update to all clients connected to a specific call
    /// </summary>
    /// <param name="callSid">The call identifier</param>
    /// <param name="status">The call status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task BroadcastCallStatusUpdateAsync(
        string callSid,
        string status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a location update to all clients connected to a specific call
    /// </summary>
    /// <param name="callSid">The call identifier</param>
    /// <param name="latitude">Latitude coordinate</param>
    /// <param name="longitude">Longitude coordinate</param>
    /// <param name="address">Optional address string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task BroadcastLocationUpdateAsync(
        string callSid,
        double latitude,
        double longitude,
        string? address = null,
        CancellationToken cancellationToken = default);
}

