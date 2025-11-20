using Microsoft.AspNetCore.SignalR;
using Prepared.Business.Interfaces;
using Prepared.Client.Hubs;

namespace Prepared.Client.Services;

/// <summary>
/// Service implementation of ITranscriptHub that broadcasts updates via SignalR
/// This service acts as a bridge between the business layer and SignalR infrastructure
/// </summary>
public class TranscriptHubService : ITranscriptHub
{
    private readonly IHubContext<TranscriptHub> _hubContext;
    private readonly ILogger<TranscriptHubService> _logger;

    public TranscriptHubService(
        IHubContext<TranscriptHub> hubContext,
        ILogger<TranscriptHubService> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task BroadcastTranscriptUpdateAsync(
        string callSid,
        string transcript,
        bool isFinal = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(callSid))
            {
                _logger.LogWarning("Attempted to broadcast transcript update with empty CallSid");
                return;
            }

            if (string.IsNullOrWhiteSpace(transcript))
            {
                _logger.LogDebug("Skipping empty transcript update for CallSid={CallSid}", callSid);
                return;
            }

            var groupName = GetCallGroup(callSid);
            
            _logger.LogDebug(
                "Broadcasting transcript update: CallSid={CallSid}, IsFinal={IsFinal}, Length={Length}",
                callSid, isFinal, transcript.Length);

            // Frontend expects (callSid, transcript, isFinal) on 'ReceiveTranscriptUpdate'
            await _hubContext.Clients
                .Group(groupName)
                .SendAsync(
                    "ReceiveTranscriptUpdate",
                    callSid,
                    transcript,
                    isFinal,
                    cancellationToken);

            _logger.LogTrace(
                "Successfully broadcast transcript update: CallSid={CallSid}",
                callSid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error broadcasting transcript update: CallSid={CallSid}",
                callSid);
            // Don't throw - we don't want to break the call processing pipeline
        }
    }

    public async Task BroadcastCallStatusUpdateAsync(
        string callSid,
        string status,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(callSid))
            {
                _logger.LogWarning("Attempted to broadcast status update with empty CallSid");
                return;
            }
            
            _logger.LogDebug(
                "Broadcasting call status update: CallSid={CallSid}, Status={Status}",
                callSid, status);

            // For new call notifications (ringing, stream_started, in-progress), 
            // broadcast to ALL clients so dashboards can discover new calls.
            // Clients will then join the call-specific group for subsequent updates.
            var isNewCallNotification = status.Equals("ringing", StringComparison.OrdinalIgnoreCase) ||
                                       status.Equals("stream_started", StringComparison.OrdinalIgnoreCase) ||
                                       status.Equals("in-progress", StringComparison.OrdinalIgnoreCase) ||
                                       status.Equals("initiated", StringComparison.OrdinalIgnoreCase);

            if (isNewCallNotification)
            {
                // Broadcast to ALL connected clients for call discovery
                _logger.LogInformation(
                    "Broadcasting new call notification to all clients: CallSid={CallSid}, Status={Status}",
                    callSid, status);

                await _hubContext.Clients
                    .All
                    .SendAsync(
                        "ReceiveCallStatusUpdate",
                        callSid,
                        status,
                        cancellationToken);
            }
            else
            {
                // For other status updates, only send to the call-specific group
                var groupName = GetCallGroup(callSid);
                
                await _hubContext.Clients
                    .Group(groupName)
                    .SendAsync(
                        "ReceiveCallStatusUpdate",
                        callSid,
                        status,
                        cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error broadcasting call status update: CallSid={CallSid}, Status={Status}",
                callSid, status);
            // Don't throw - we don't want to break the call processing pipeline
        }
    }

    public async Task BroadcastLocationUpdateAsync(
        string callSid,
        double latitude,
        double longitude,
        string? address = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(callSid))
            {
                _logger.LogWarning("Attempted to broadcast location update with empty CallSid");
                return;
            }

            var groupName = GetCallGroup(callSid);
            
            _logger.LogInformation(
                "Broadcasting location update: CallSid={CallSid}, Lat={Latitude}, Lng={Longitude}, Address={Address}",
                callSid, latitude, longitude, address);

            // Frontend expects (callSid, latitude, longitude, address) on 'ReceiveLocationUpdate'
            await _hubContext.Clients
                .Group(groupName)
                .SendAsync(
                    "ReceiveLocationUpdate",
                    callSid,
                    latitude,
                    longitude,
                    address,
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error broadcasting location update: CallSid={CallSid}",
                callSid);
            // Don't throw - we don't want to break the call processing pipeline
        }
    }

    public async Task BroadcastSummaryUpdateAsync(
        string callSid,
        string summary,
        IEnumerable<string>? keyFindings = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(callSid))
            {
                _logger.LogWarning("Attempted to broadcast summary with empty CallSid");
                return;
            }

            if (string.IsNullOrWhiteSpace(summary))
            {
                return;
            }

            var groupName = GetCallGroup(callSid);

            // Frontend expects (callSid, summary, keyFindings) on 'ReceiveSummaryUpdate'
            await _hubContext.Clients
                .Group(groupName)
                .SendAsync(
                    "ReceiveSummaryUpdate",
                    callSid,
                    summary,
                    keyFindings ?? Array.Empty<string>(),
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error broadcasting summary: CallSid={CallSid}",
                callSid);
        }
    }

    private static string GetCallGroup(string callSid)
    {
        return $"call_{callSid}";
    }
}

