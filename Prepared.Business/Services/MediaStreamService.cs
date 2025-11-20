using Microsoft.Extensions.Logging;
using Prepared.Business.Interfaces;
using Prepared.Data.Interfaces;

namespace Prepared.Business.Services;

/// <summary>
/// Service for handling Twilio Media Streams with production-ready error handling
/// </summary>
public class MediaStreamService : IMediaStreamService
{
    private readonly ILogger<MediaStreamService> _logger;
    private readonly ITranscriptHub _transcriptHub;
    private readonly ITranscriptionService _transcriptionService;
    private readonly ISummarizationService _summarizationService;
    private readonly ILocationExtractionService _locationExtractionService;
    private readonly ICallRepository _callRepository;
    private readonly ITranscriptRepository _transcriptRepository;
    private readonly ISummaryRepository _summaryRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly Dictionary<string, DateTime> _activeStreams = new();
    private readonly Dictionary<string, string> _streamToCallMapping = new(); // Maps StreamSid to CallSid
    private readonly Dictionary<string, List<string>> _transcriptBuffers = new(); // CallSid => transcript segments
    private readonly Dictionary<string, int> _transcriptSequenceNumbers = new(); // CallSid => sequence number
    private readonly Dictionary<string, List<byte>> _audioBuffers = new(); // StreamSid => buffered mu-law audio

    public MediaStreamService(
        ILogger<MediaStreamService> logger,
        ITranscriptHub transcriptHub,
        ITranscriptionService transcriptionService,
        ISummarizationService summarizationService,
        ILocationExtractionService locationExtractionService,
        ICallRepository callRepository,
        ITranscriptRepository transcriptRepository,
        ISummaryRepository summaryRepository,
        ILocationRepository locationRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _transcriptHub = transcriptHub ?? throw new ArgumentNullException(nameof(transcriptHub));
        _transcriptionService = transcriptionService ?? throw new ArgumentNullException(nameof(transcriptionService));
        _summarizationService = summarizationService ?? throw new ArgumentNullException(nameof(summarizationService));
        _locationExtractionService = locationExtractionService ?? throw new ArgumentNullException(nameof(locationExtractionService));
        _callRepository = callRepository ?? throw new ArgumentNullException(nameof(callRepository));
        _transcriptRepository = transcriptRepository ?? throw new ArgumentNullException(nameof(transcriptRepository));
        _summaryRepository = summaryRepository ?? throw new ArgumentNullException(nameof(summaryRepository));
        _locationRepository = locationRepository ?? throw new ArgumentNullException(nameof(locationRepository));
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
            _audioBuffers[streamSid] = new List<byte>();
            _audioBuffers[streamSid] = new List<byte>();

            // Update call record with stream information
            try
            {
                await _callRepository.UpdateStreamInfoAsync(callSid, streamSid, hasActiveStream: true, cancellationToken);
                _logger.LogDebug("Updated call stream info: CallSid={CallSid}, StreamSid={StreamSid}", callSid, streamSid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update call stream info: CallSid={CallSid}, StreamSid={StreamSid}", callSid, streamSid);
            }

            // Initialize sequence number for this call
            _transcriptSequenceNumbers[callSid] = 0;

            // Update call status to InProgress once media stream starts
            try
            {
                await _callRepository.UpdateStatusAsync(callSid, "in-progress", cancellationToken);
                _logger.LogDebug("Updated call status to in-progress: CallSid={CallSid}", callSid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update call status to in-progress: CallSid={CallSid}", callSid);
            }

            // Notify connected clients that the stream has started
            await _transcriptHub.BroadcastCallStatusUpdateAsync(
                callSid,
                "stream_started",
                cancellationToken);
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

                // Buffer audio so we don't send sub-0.1s chunks to OpenAI
                if (!_audioBuffers.TryGetValue(streamSid, out var audioBuffer))
                {
                    audioBuffer = new List<byte>();
                    _audioBuffers[streamSid] = audioBuffer;
                }

                audioBuffer.AddRange(audioBytes);

                // At 8kHz mu-law, 0.1s ≈ 800 bytes. Use a slightly larger threshold to be safe.
                const int minBytesForTranscription = 8000; // ~1 second of 8kHz μ-law audio (better for Whisper)
                if (audioBuffer.Count < minBytesForTranscription)
                {
                    _logger.LogDebug(
                        "Buffered media data below threshold: StreamSid={StreamSid}, BufferedSize={Size} bytes",
                        streamSid, audioBuffer.Count);
                    return;
                }

                var bufferedAudio = audioBuffer.ToArray();
                audioBuffer.Clear();

                if (_streamToCallMapping.TryGetValue(streamSid, out var callSid))
                {
                    var transcriptionResult = await _transcriptionService.TranscribeAsync(
                        callSid,
                        streamSid,
                        bufferedAudio,
                        isFinal: false,
                        cancellationToken);

                    if (transcriptionResult?.Text is { Length: > 0 } text)
                    {
                        AppendTranscript(callSid, text);

                        // Save transcript chunk to storage
                        try
                        {
                            var sequenceNumber = _transcriptSequenceNumbers.GetValueOrDefault(callSid, 0);
                            await _transcriptRepository.SaveAsync(transcriptionResult, sequenceNumber, cancellationToken);
                            _transcriptSequenceNumbers[callSid] = sequenceNumber + 1;
                            _logger.LogDebug("Saved transcript chunk: CallSid={CallSid}, IsFinal={IsFinal}, Sequence={Sequence}",
                                callSid, transcriptionResult.IsFinal, sequenceNumber);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to save transcript chunk: CallSid={CallSid}", callSid);
                        }

                        await _transcriptHub.BroadcastTranscriptUpdateAsync(
                            callSid,
                            text,
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

            // Update call record to remove stream information
            try
            {
                await _callRepository.UpdateStreamInfoAsync(callSid, streamSid: null, hasActiveStream: false, cancellationToken);
                _logger.LogDebug("Updated call stream info (stopped): CallSid={CallSid}", callSid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update call stream info: CallSid={CallSid}", callSid);
            }

            // Clean up mappings
            _streamToCallMapping.Remove(streamSid);
            _transcriptSequenceNumbers.Remove(callSid);
            _audioBuffers.Remove(streamSid);
            await GenerateInsightsAsync(callSid, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling stream stop: StreamSid={StreamSid}, CallSid={CallSid}",
                streamSid, callSid);
            throw;
        }
    }

    private void AppendTranscript(string callSid, string text)
    {
        if (!_transcriptBuffers.TryGetValue(callSid, out var segments))
        {
            segments = new List<string>();
            _transcriptBuffers[callSid] = segments;
        }

        segments.Add(text);
    }

    private async Task GenerateInsightsAsync(string callSid, CancellationToken cancellationToken)
    {
        if (!_transcriptBuffers.TryGetValue(callSid, out var segments) || segments.Count == 0)
        {
            return;
        }

        var transcriptText = string.Join(" ", segments);
        _transcriptBuffers.Remove(callSid);

        try
        {
            var summaryTask = _summarizationService.SummarizeAsync(callSid, transcriptText, cancellationToken);
            var locationTask = _locationExtractionService.ExtractAsync(callSid, transcriptText, cancellationToken);

            var summary = await summaryTask;
            if (summary != null)
            {
                // Save summary to storage
                try
                {
                    await _summaryRepository.UpsertAsync(summary, cancellationToken);
                    _logger.LogDebug("Saved summary: CallSid={CallSid}", callSid);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save summary: CallSid={CallSid}", callSid);
                }

                await _transcriptHub.BroadcastSummaryUpdateAsync(
                    callSid,
                    summary.Summary,
                    summary.KeyFindings,
                    cancellationToken);
            }

            var location = await locationTask;
            if (location?.Latitude is double lat && location.Longitude is double lng)
            {
                // Save location to storage
                try
                {
                    await _locationRepository.UpsertAsync(location, cancellationToken);
                    _logger.LogDebug("Saved location: CallSid={CallSid}, Lat={Latitude}, Lng={Longitude}", callSid, lat, lng);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save location: CallSid={CallSid}", callSid);
                }

                await _transcriptHub.BroadcastLocationUpdateAsync(
                    callSid,
                    lat,
                    lng,
                    location.FormattedAddress,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating insights for CallSid={CallSid}", callSid);
        }
    }
}

