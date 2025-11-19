using Microsoft.Extensions.Logging;
using Prepared.Business.Interfaces;

namespace Prepared.Business.Services;

/// <summary>
/// Service for handling Twilio Media Streams with production-ready error handling
/// </summary>
public class MediaStreamService : IMediaStreamService
{
    private readonly ILogger<MediaStreamService> _logger;
    private readonly ITranscriptHub _transcriptHub;
    private readonly ITranscriptionService _transcriptionService;
    private readonly Dictionary<string, DateTime> _activeStreams = new();
    private readonly Dictionary<string, string> _streamToCallMapping = new(); // Maps StreamSid to CallSid

    public MediaStreamService(
        ILogger<MediaStreamService> logger,
        ITranscriptHub transcriptHub,
        ITranscriptionService transcriptionService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _transcriptHub = transcriptHub ?? throw new ArgumentNullException(nameof(transcriptHub));
        _transcriptionService = transcriptionService ?? throw new ArgumentNullException(nameof(transcriptionService));
    }

    public async Task HandleStreamStartAsync(string streamSid, string callSid, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Media stream started: StreamSid={StreamSid}, CallSid={CallSid}",
                streamSid, callSid);

            _activeStreams[streamSid] = DateTime.UtcNow;
            _streamToCallMapping[streamSid] = callSid;

            // Notify connected clients that the stream has started
            await _transcriptHub.BroadcastCallStatusUpdateAsync(
                callSid,
                "stream_started",
                cancellationToken);

            // Here you would typically:
            // 1. Create a stream record in the database
            // 2. Initialize transcription service
            // 3. Set up any real-time processing pipelines

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling stream start: StreamSid={StreamSid}, CallSid={CallSid}",
                streamSid, callSid);
            throw;
        }
    }

    public async Task ProcessMediaDataAsync(
        string streamSid,
        string? mediaPayload,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_activeStreams.ContainsKey(streamSid))
            {
                _logger.LogWarning(
                    "Received media data for unknown stream: StreamSid={StreamSid}",
                    streamSid);
                return;
            }

            if (eventType == "media" && !string.IsNullOrWhiteSpace(mediaPayload))
            {
                // Decode the base64 audio payload
                var audioBytes = Convert.FromBase64String(mediaPayload);

                _logger.LogDebug(
                    "Processing media data: StreamSid={StreamSid}, PayloadSize={Size} bytes",
                    streamSid, audioBytes.Length);

                if (_streamToCallMapping.TryGetValue(streamSid, out var callSid))
                {
                    var transcriptionResult = await _transcriptionService.TranscribeAsync(
                        callSid,
                        streamSid,
                        audioBytes,
                        isFinal: false,
                        cancellationToken);

                    if (transcriptionResult?.Text is { Length: > 0 })
                    {
                        await _transcriptHub.BroadcastTranscriptUpdateAsync(
                            callSid,
                            transcriptionResult.Text,
                            transcriptionResult.IsFinal,
                            cancellationToken);
                    }
                }
                else
                {
                    _logger.LogDebug("No call mapping found for StreamSid={StreamSid}", streamSid);
                }
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing media data: StreamSid={StreamSid}, EventType={EventType}",
                streamSid, eventType);
            // Don't throw - we want to continue processing even if one chunk fails
        }
    }

    public async Task HandleStreamStopAsync(string streamSid, string callSid, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Media stream stopped: StreamSid={StreamSid}, CallSid={CallSid}",
                streamSid, callSid);

            if (_activeStreams.TryGetValue(streamSid, out var startTime))
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Stream duration: StreamSid={StreamSid}, Duration={Duration}",
                    streamSid, duration);
                
                _activeStreams.Remove(streamSid);
            }

            // Notify connected clients that the stream has ended
            await _transcriptHub.BroadcastCallStatusUpdateAsync(
                callSid,
                "stream_stopped",
                cancellationToken);

            // Clean up mappings
            _streamToCallMapping.Remove(streamSid);

            // Here you would typically:
            // 1. Finalize the transcript
            // 2. Update the stream record in the database
            // 3. Trigger location extraction from final transcript
            // 4. Clean up any resources

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling stream stop: StreamSid={StreamSid}, CallSid={CallSid}",
                streamSid, callSid);
            throw;
        }
    }
}

