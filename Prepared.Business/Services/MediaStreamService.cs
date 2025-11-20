using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prepared.Business.Interfaces;
using Prepared.Business.Options;
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
    private readonly IUnifiedInsightsService _unifiedInsightsService;
    private readonly ICallRepository _callRepository;
    private readonly ITranscriptRepository _transcriptRepository;
    private readonly ISummaryRepository _summaryRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly MediaStreamOptions _options;
    private readonly Dictionary<string, DateTime> _activeStreams = new();
    private readonly Dictionary<string, string> _streamToCallMapping = new(); // Maps StreamSid to CallSid
    private readonly Dictionary<string, List<string>> _transcriptBuffers = new(); // CallSid => transcript segments
    private readonly Dictionary<string, int> _transcriptSequenceNumbers = new(); // CallSid => sequence number
    private readonly Dictionary<string, List<byte>> _audioBuffers = new(); // StreamSid => buffered mu-law audio
    private readonly Dictionary<string, DateTime> _lastLocationCheckTime = new(); // CallSid => last check time
    private readonly Dictionary<string, bool> _locationFound = new(); // CallSid => location already found

    public MediaStreamService(
        ILogger<MediaStreamService> logger,
        ITranscriptHub transcriptHub,
        ITranscriptionService transcriptionService,
        ISummarizationService summarizationService,
        ILocationExtractionService locationExtractionService,
        IUnifiedInsightsService unifiedInsightsService,
        ICallRepository callRepository,
        ITranscriptRepository transcriptRepository,
        ISummaryRepository summaryRepository,
        ILocationRepository locationRepository,
        IOptions<MediaStreamOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _transcriptHub = transcriptHub ?? throw new ArgumentNullException(nameof(transcriptHub));
        _transcriptionService = transcriptionService ?? throw new ArgumentNullException(nameof(transcriptionService));
        _summarizationService = summarizationService ?? throw new ArgumentNullException(nameof(summarizationService));
        _locationExtractionService = locationExtractionService ?? throw new ArgumentNullException(nameof(locationExtractionService));
        _unifiedInsightsService = unifiedInsightsService ?? throw new ArgumentNullException(nameof(unifiedInsightsService));
        _callRepository = callRepository ?? throw new ArgumentNullException(nameof(callRepository));
        _transcriptRepository = transcriptRepository ?? throw new ArgumentNullException(nameof(transcriptRepository));
        _summaryRepository = summaryRepository ?? throw new ArgumentNullException(nameof(summaryRepository));
        _locationRepository = locationRepository ?? throw new ArgumentNullException(nameof(locationRepository));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
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

                // Calculate minimum bytes based on configured buffer duration
                // At 8kHz μ-law (8-bit, mono), 1 second ≈ 8000 bytes
                var minBytesForTranscription = (int)(_options.SampleRate * _options.AudioBufferSeconds);
                if (audioBuffer.Count < minBytesForTranscription)
                {
                    _logger.LogDebug(
                        "Buffered media data below threshold: StreamSid={StreamSid}, BufferedSize={Size} bytes",
                        streamSid, audioBuffer.Count);
                    return;
                }

                var bufferedAudio = audioBuffer.ToArray();
                audioBuffer.Clear();

                // Skip silent chunks to avoid gibberish transcriptions
                if (IsSilentChunk(bufferedAudio))
                {
                    _logger.LogDebug(
                        "Skipping silent audio chunk: StreamSid={StreamSid}, BufferedSize={Size} bytes",
                        streamSid, bufferedAudio.Length);
                    return;
                }

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

                        // Real-time location extraction - check periodically as transcript builds up
                        await TryExtractLocationRealTimeAsync(callSid, cancellationToken);
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

            // Process any remaining buffered audio before stopping
            if (_audioBuffers.TryGetValue(streamSid, out var remainingAudio) && remainingAudio.Count > 0)
            {
                _logger.LogInformation(
                    "Processing final buffered audio chunk: StreamSid={StreamSid}, BufferedSize={Size} bytes",
                    streamSid, remainingAudio.Count);

                var finalAudio = remainingAudio.ToArray();
                if (!IsSilentChunk(finalAudio))
                {
                    var transcriptionResult = await _transcriptionService.TranscribeAsync(
                        callSid,
                        streamSid,
                        finalAudio,
                        isFinal: true,
                        cancellationToken);

                    if (transcriptionResult?.Text is { Length: > 0 } text)
                    {
                        AppendTranscript(callSid, text);

                        try
                        {
                            var sequenceNumber = _transcriptSequenceNumbers.GetValueOrDefault(callSid, 0);
                            await _transcriptRepository.SaveAsync(transcriptionResult, sequenceNumber, cancellationToken);
                            _transcriptSequenceNumbers[callSid] = sequenceNumber + 1;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to save final transcript chunk: CallSid={CallSid}", callSid);
                        }

                        await _transcriptHub.BroadcastTranscriptUpdateAsync(
                            callSid,
                            text,
                            transcriptionResult.IsFinal,
                            cancellationToken);
                    }
                }
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
            _lastLocationCheckTime.Remove(callSid);
            _locationFound.Remove(callSid);
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

    /// <summary>
    /// Real-time location extraction - checks periodically as transcript builds up.
    /// This enables 911-style real-time location detection while the caller is speaking.
    /// </summary>
    private async Task TryExtractLocationRealTimeAsync(string callSid, CancellationToken cancellationToken)
    {
        try
        {
            // Skip if we already found a location for this call
            if (_locationFound.GetValueOrDefault(callSid, false))
            {
                return;
            }

            // Rate limit: only check every 5 seconds to avoid excessive API calls
            var now = DateTime.UtcNow;
            if (_lastLocationCheckTime.TryGetValue(callSid, out var lastCheck))
            {
                if ((now - lastCheck).TotalSeconds < 5)
                {
                    return; // Too soon since last check
                }
            }

            // Need at least 8 words before trying to extract location
            // Example: "I'm at 123 Main Street there's a fire" = 9 words
            if (!_transcriptBuffers.TryGetValue(callSid, out var segments) || segments.Count == 0)
            {
                return;
            }

            var transcriptSoFar = string.Join(" ", segments);
            var wordCount = transcriptSoFar.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

            if (wordCount < 8)
            {
                _logger.LogDebug(
                    "Skipping location check - not enough words yet: CallSid={CallSid}, Words={WordCount}",
                    callSid, wordCount);
                return;
            }

            _lastLocationCheckTime[callSid] = now;

            _logger.LogInformation(
                "Attempting real-time UNIFIED insights extraction: CallSid={CallSid}, TranscriptLength={Length}, Words={WordCount}",
                callSid, transcriptSoFar.Length, wordCount);

            // Use unified insights service - extracts location AND summary in ONE call!
            var insights = await _unifiedInsightsService.ExtractInsightsAsync(callSid, transcriptSoFar, cancellationToken);

            if (insights?.Location is { Latitude: double lat, Longitude: double lng })
            {
                _logger.LogInformation(
                    "🎯 REAL-TIME location found! CallSid={CallSid}, Address={Address}, Lat={Lat}, Lng={Lng}",
                    callSid, insights.Location.FormattedAddress, lat, lng);

                // Mark as found so we don't keep checking
                _locationFound[callSid] = true;

                // Save location to storage
                try
                {
                    await _locationRepository.UpsertAsync(insights.Location, cancellationToken);
                    _logger.LogInformation("Saved real-time location: CallSid={CallSid}", callSid);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save real-time location: CallSid={CallSid}", callSid);
                }

                // Broadcast location immediately to UI - pin drops in real-time!
                await _transcriptHub.BroadcastLocationUpdateAsync(
                    callSid,
                    lat,
                    lng,
                    insights.Location.FormattedAddress,
                    cancellationToken);
            }

            // Also broadcast summary & key findings if available (bonus from unified call!)
            if (insights?.Summary != null)
            {
                _logger.LogInformation(
                    "📝 REAL-TIME summary extracted: CallSid={CallSid}, KeyFindings={Count}",
                    callSid, insights.Summary.KeyFindings?.Count ?? 0);

                // Save summary
                try
                {
                    await _summaryRepository.UpsertAsync(insights.Summary, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save real-time summary: CallSid={CallSid}", callSid);
                }

                // Broadcast summary immediately
                await _transcriptHub.BroadcastSummaryUpdateAsync(
                    callSid,
                    insights.Summary.Summary,
                    insights.Summary.KeyFindings,
                    cancellationToken);
            }
            else
            {
                _logger.LogDebug(
                    "No location found yet in real-time check: CallSid={CallSid}, Words={WordCount}",
                    callSid, wordCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in real-time location extraction: CallSid={CallSid}", callSid);
            // Don't throw - we don't want to break the transcription pipeline
        }
    }

    private async Task GenerateInsightsAsync(string callSid, CancellationToken cancellationToken)
    {
        if (!_transcriptBuffers.TryGetValue(callSid, out var segments) || segments.Count == 0)
        {
            _logger.LogWarning(
                "No transcript segments found for insights generation: CallSid={CallSid}",
                callSid);
            return;
        }

        var transcriptText = string.Join(" ", segments);
        _transcriptBuffers.Remove(callSid);

        _logger.LogInformation(
            "Generating insights for call: CallSid={CallSid}, TranscriptLength={Length}, Segments={SegmentCount}",
            callSid, transcriptText.Length, segments.Count);
        _logger.LogInformation("Full transcript for insights: {Transcript}", transcriptText);

        try
        {
            // Use unified insights service - extracts location, summary, and key findings in ONE call!
            var insights = await _unifiedInsightsService.ExtractInsightsAsync(callSid, transcriptText, cancellationToken);

            if (insights == null)
            {
                _logger.LogWarning("Unified insights extraction returned null: CallSid={CallSid}", callSid);
                return;
            }

            // Process summary & key findings
            if (insights.Summary != null)
            {
                _logger.LogInformation(
                    "Final summary extracted: CallSid={CallSid}, Summary={Summary}, KeyFindings={Count}",
                    callSid, insights.Summary.Summary, insights.Summary.KeyFindings?.Count ?? 0);

                // Save summary to storage
                try
                {
                    await _summaryRepository.UpsertAsync(insights.Summary, cancellationToken);
                    _logger.LogDebug("Saved final summary: CallSid={CallSid}", callSid);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save final summary: CallSid={CallSid}", callSid);
                }

                // Broadcast summary immediately
                await _transcriptHub.BroadcastSummaryUpdateAsync(
                    callSid,
                    insights.Summary.Summary,
                    insights.Summary.KeyFindings,
                    cancellationToken);
            }

            // Process location
            if (insights.Location != null)
            {
                _logger.LogInformation(
                    "Final location extraction result: CallSid={CallSid}, Address={Address}, HasCoordinates={HasCoords}, Lat={Lat}, Lng={Lng}",
                    callSid, insights.Location.FormattedAddress,
                    insights.Location.Latitude.HasValue && insights.Location.Longitude.HasValue,
                    insights.Location.Latitude, insights.Location.Longitude);

                // Save location to storage if we have coordinates
                if (insights.Location.Latitude is double lat && insights.Location.Longitude is double lng)
                {
                    try
                    {
                        await _locationRepository.UpsertAsync(insights.Location, cancellationToken);
                        _logger.LogInformation("Saved final location: CallSid={CallSid}, Lat={Latitude}, Lng={Longitude}", callSid, lat, lng);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to save final location: CallSid={CallSid}", callSid);
                    }

                    // Broadcast location immediately
                    await _transcriptHub.BroadcastLocationUpdateAsync(
                        callSid,
                        lat,
                        lng,
                        insights.Location.FormattedAddress,
                        cancellationToken);
                }
                else if (!string.IsNullOrWhiteSpace(insights.Location.FormattedAddress))
                {
                    // Address found but no coordinates - save it anyway, UI can display address text
                    _logger.LogInformation(
                        "Location address found but no coordinates: CallSid={CallSid}, Address={Address}",
                        callSid, insights.Location.FormattedAddress);
                    
                    try
                    {
                        await _locationRepository.UpsertAsync(insights.Location, cancellationToken);
                        _logger.LogDebug("Saved location address (no coordinates): CallSid={CallSid}", callSid);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to save location address: CallSid={CallSid}", callSid);
                    }
                    // Note: We don't broadcast location without coordinates since UI requires lat/lng for map markers
                }
            }
            else
            {
                _logger.LogInformation(
                    "No location found in transcript: CallSid={CallSid}, TranscriptLength={Length}",
                    callSid, transcriptText.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating insights for CallSid={CallSid}", callSid);
        }
    }

    /// <summary>
    /// Checks if an audio chunk is effectively silent (mostly μ-law 0xFF or 0x7F values).
    /// μ-law silence is typically 0xFF (255) or 0x7F (127).
    /// </summary>
    private bool IsSilentChunk(byte[] muLawAudio)
    {
        if (muLawAudio.Length == 0)
            return true;

        // Count how many bytes are near silence (0xFF or 0x7F, ±2)
        var silentCount = 0;
        foreach (var b in muLawAudio)
        {
            if (b >= 0xFD || (b >= 0x7D && b <= 0x81))
                silentCount++;
        }

        // Use configurable threshold
        var silenceRatio = (double)silentCount / muLawAudio.Length;
        return silenceRatio > _options.SilenceThreshold;
    }
}

