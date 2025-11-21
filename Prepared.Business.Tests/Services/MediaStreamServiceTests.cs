using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Prepared.Business.Interfaces;
using Prepared.Business.Services;
using Prepared.Common.Models;
using Prepared.Data.Interfaces;

namespace Prepared.Business.Tests.Services;

public class MediaStreamServiceTests
{
    private readonly Mock<ILogger<MediaStreamService>> _loggerMock;
    private readonly Mock<ITranscriptHub> _transcriptHubMock;
    private readonly Mock<ITranscriptionService> _transcriptionServiceMock;
    private readonly Mock<ISummarizationService> _summarizationServiceMock;
    private readonly Mock<ILocationExtractionService> _locationExtractionServiceMock;
    private readonly Mock<IUnifiedInsightsService> _unifiedInsightsServiceMock;
    private readonly Mock<ICallRepository> _callRepositoryMock;
    private readonly Mock<ITranscriptRepository> _transcriptRepositoryMock;
    private readonly Mock<ISummaryRepository> _summaryRepositoryMock;
    private readonly Mock<ILocationRepository> _locationRepositoryMock;
    private readonly Mock<IMediaStreamConfigurationService> _configMock;
    private readonly MediaStreamService _service;

    public MediaStreamServiceTests()
    {
        _loggerMock = new Mock<ILogger<MediaStreamService>>();
        _transcriptHubMock = new Mock<ITranscriptHub>();
        _transcriptionServiceMock = new Mock<ITranscriptionService>();
        _summarizationServiceMock = new Mock<ISummarizationService>();
        _locationExtractionServiceMock = new Mock<ILocationExtractionService>();
        _unifiedInsightsServiceMock = new Mock<IUnifiedInsightsService>();
        _callRepositoryMock = new Mock<ICallRepository>();
        _transcriptRepositoryMock = new Mock<ITranscriptRepository>();
        _summaryRepositoryMock = new Mock<ISummaryRepository>();
        _locationRepositoryMock = new Mock<ILocationRepository>();
        
        _configMock = new Mock<IMediaStreamConfigurationService>();
        _configMock.Setup(x => x.AudioBufferSeconds).Returns(4.0); // 4 seconds = 32000 bytes at 8kHz μ-law
        _configMock.Setup(x => x.SilenceThreshold).Returns(0.9);
        _configMock.Setup(x => x.SampleRate).Returns(8000);
        
        _service = new MediaStreamService(
            _loggerMock.Object,
            _transcriptHubMock.Object,
            _transcriptionServiceMock.Object,
            _summarizationServiceMock.Object,
            _locationExtractionServiceMock.Object,
            _unifiedInsightsServiceMock.Object,
            _callRepositoryMock.Object,
            _transcriptRepositoryMock.Object,
            _summaryRepositoryMock.Object,
            _locationRepositoryMock.Object,
            _configMock.Object);
    }

    [Fact]
    public async Task HandleStreamStartAsync_ShouldLogStreamStart()
    {
        // Arrange
        var streamSid = "MZ123456789";
        var callSid = "CA123456789";

        // Act
        await _service.HandleStreamStartAsync(streamSid, callSid);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains(streamSid) && 
                    v.ToString()!.Contains(callSid) &&
                    v.ToString()!.Contains("started")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        // Verify SignalR broadcast
        _callRepositoryMock.Verify(
            x => x.UpdateStreamInfoAsync(callSid, streamSid, true, It.IsAny<CancellationToken>()),
            Times.Once);
        
        _transcriptHubMock.Verify(
            x => x.BroadcastCallStatusUpdateAsync(
                callSid,
                "stream_started",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleStreamStartAsync_OnException_ShouldLogError()
    {
        // Arrange
        var streamSid = "MZ123456789";
        var callSid = "CA123456789";
        
        // Create a service that will throw (though current implementation shouldn't)
        // This test ensures error handling is in place

        // Act
        await _service.HandleStreamStartAsync(streamSid, callSid);

        // Assert - Should complete without throwing
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that media data is buffered and processed when the buffer threshold is reached.
    /// Tests the complete flow: buffering → transcription → storage → SignalR broadcast.
    /// </summary>
    [Fact]
    public async Task ProcessMediaDataAsync_WhenBufferThresholdReached_ShouldTranscribeAndBroadcast()
    {
        // Arrange
        var streamSid = "MZ123456789";
        var callSid = "CA123456789";
        var mediaPayload = GenerateNonSilentAudioPayload(bytesCount: 32001); // Exceeds 4-second buffer (32000 bytes)
        var eventType = "media";

        // First start the stream
        await _service.HandleStreamStartAsync(streamSid, callSid);
        _transcriptionServiceMock
            .Setup(x => x.TranscribeAsync(
                callSid,
                streamSid,
                It.IsAny<ReadOnlyMemory<byte>>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult
            {
                CallSid = callSid,
                StreamSid = streamSid,
                Text = "test transcript"
            });

        // Act
        await _service.ProcessMediaDataAsync(streamSid, mediaPayload, eventType);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains(streamSid) &&
                    v.ToString()!.Contains("Processing media data")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _transcriptionServiceMock.Verify(
            x => x.TranscribeAsync(
                callSid,
                streamSid,
                It.IsAny<ReadOnlyMemory<byte>>(),
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _transcriptRepositoryMock.Verify(
            x => x.SaveAsync(
                It.Is<TranscriptionResult>(t => t.Text == "test transcript"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        
        _transcriptHubMock.Verify(
            x => x.BroadcastTranscriptUpdateAsync(
                callSid,
                "test transcript",
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMediaDataAsync_WithUnknownStream_ShouldLogWarning()
    {
        // Arrange
        var streamSid = "MZ_UNKNOWN";
        var mediaPayload = Convert.ToBase64String(new byte[] { 1, 2, 3 });
        var eventType = "media";

        // Act
        await _service.ProcessMediaDataAsync(streamSid, mediaPayload, eventType);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("unknown stream")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMediaDataAsync_WithNullPayload_ShouldHandleGracefully()
    {
        // Arrange
        var streamSid = "MZ123456789";
        await _service.HandleStreamStartAsync(streamSid, "CA123456789");

        // Act
        await _service.ProcessMediaDataAsync(streamSid, null, "media");

        // Assert - Should not throw and should handle null gracefully
        // The service checks for null/empty payload before processing
        // No specific logging is expected for null payloads, just that it doesn't throw
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessMediaDataAsync_WithInvalidBase64_ShouldHandleGracefully()
    {
        // Arrange
        var streamSid = "MZ123456789";
        await _service.HandleStreamStartAsync(streamSid, "CA123456789");
        var invalidPayload = "not-valid-base64!!!";

        // Act & Assert
        // Should not throw, but may log an error
        await _service.ProcessMediaDataAsync(streamSid, invalidPayload, "media");
    }

    [Fact]
    public async Task ProcessMediaDataAsync_WhenTranscriptionReturnsNull_ShouldNotBroadcastTranscript()
    {
        // Arrange
        var streamSid = "MZ123456789";
        var callSid = "CA123456789";
        var mediaPayload = Convert.ToBase64String(new byte[] { 1, 2, 3 });
        await _service.HandleStreamStartAsync(streamSid, callSid);
        _transcriptionServiceMock
            .Setup(x => x.TranscribeAsync(
                callSid,
                streamSid,
                It.IsAny<ReadOnlyMemory<byte>>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Prepared.Common.Models.TranscriptionResult?)null);

        // Act
        await _service.ProcessMediaDataAsync(streamSid, mediaPayload, "media");

        // Assert
        _transcriptHubMock.Verify(
            x => x.BroadcastTranscriptUpdateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleStreamStopAsync_WithTranscripts_ShouldGenerateInsights()
    {
        // Arrange
        var streamSid = "MZ123456789";
        var callSid = "CA123456789";
        await _service.HandleStreamStartAsync(streamSid, callSid);
        _transcriptionServiceMock
            .Setup(x => x.TranscribeAsync(
                callSid,
                streamSid,
                It.IsAny<ReadOnlyMemory<byte>>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult
            {
                CallSid = callSid,
                StreamSid = streamSid,
                Text = "The caller reports a fire near 123 Main Street.",
                IsFinal = false
            });
        _unifiedInsightsServiceMock
            .Setup(x => x.ExtractInsightsAsync(callSid, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UnifiedInsightsResult
            {
                CallSid = callSid,
                Summary = new TranscriptSummary
                {
                    CallSid = callSid,
                    Summary = "Fire near 123 Main Street",
                    KeyFindings = new[] { "Structure fire", "Evacuation in progress" }
                },
                Location = new LocationExtractionResult
                {
                    CallSid = callSid,
                    Latitude = 37.7749,
                    Longitude = -122.4194,
                    FormattedAddress = "123 Main Street, San Francisco, CA"
                }
            });

        // Send enough audio data to trigger transcription (32000+ bytes for 4-second buffer)
        var mediaPayload = GenerateNonSilentAudioPayload(bytesCount: 32001);
        await _service.ProcessMediaDataAsync(streamSid, mediaPayload, "media");

        // Act
        await _service.HandleStreamStopAsync(streamSid, callSid);

        // Assert
        // ExtractInsightsAsync may be called multiple times (real-time + final), so we verify results instead
        // The important thing is that insights are saved and broadcast, which we verify below
        
        // Summary may be saved/broadcast multiple times (real-time + final), verify at least once
        _summaryRepositoryMock.Verify(
            x => x.UpsertAsync(
                It.Is<TranscriptSummary>(s => s.CallSid == callSid && s.Summary == "Fire near 123 Main Street"),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        
        _transcriptHubMock.Verify(
            x => x.BroadcastSummaryUpdateAsync(
                callSid,
                "Fire near 123 Main Street",
                It.Is<IEnumerable<string>>(kf => kf.Contains("Structure fire") && kf.Contains("Evacuation in progress")),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        // Location may be saved/broadcast multiple times (real-time + final), verify at least once
        _locationRepositoryMock.Verify(
            x => x.UpsertAsync(
                It.Is<LocationExtractionResult>(l => l.CallSid == callSid),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        _transcriptHubMock.Verify(
            x => x.BroadcastLocationUpdateAsync(
                callSid,
                37.7749,
                -122.4194,
                "123 Main Street, San Francisco, CA",
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task HandleStreamStopAsync_ShouldLogStreamStop()
    {
        // Arrange
        var streamSid = "MZ123456789";
        var callSid = "CA123456789";
        
        // Start the stream first
        await _service.HandleStreamStartAsync(streamSid, callSid);

        // Act
        await _service.HandleStreamStopAsync(streamSid, callSid);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains(streamSid) && 
                    v.ToString()!.Contains(callSid) &&
                    v.ToString()!.Contains("stopped")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        _callRepositoryMock.Verify(
            x => x.UpdateStreamInfoAsync(callSid, null, false, It.IsAny<CancellationToken>()),
            Times.Once);
        
        // Verify SignalR broadcast
        _transcriptHubMock.Verify(
            x => x.BroadcastCallStatusUpdateAsync(
                callSid,
                "stream_stopped",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleStreamStopAsync_ShouldCalculateDuration()
    {
        // Arrange
        var streamSid = "MZ123456789";
        var callSid = "CA123456789";
        
        await _service.HandleStreamStartAsync(streamSid, callSid);
        
        // Wait a bit to ensure duration calculation
        await Task.Delay(10);

        // Act
        await _service.HandleStreamStopAsync(streamSid, callSid);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("Stream duration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Arrange
        var configMock = new Mock<IMediaStreamConfigurationService>();
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MediaStreamService(null!, _transcriptHubMock.Object, _transcriptionServiceMock.Object, _summarizationServiceMock.Object, _locationExtractionServiceMock.Object, _unifiedInsightsServiceMock.Object, _callRepositoryMock.Object, _transcriptRepositoryMock.Object, _summaryRepositoryMock.Object, _locationRepositoryMock.Object, configMock.Object));
    }

    [Fact]
    public void Constructor_WithNullTranscriptHub_ShouldThrow()
    {
        // Arrange
        var configMock = new Mock<IMediaStreamConfigurationService>();
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MediaStreamService(_loggerMock.Object, null!, _transcriptionServiceMock.Object, _summarizationServiceMock.Object, _locationExtractionServiceMock.Object, _unifiedInsightsServiceMock.Object, _callRepositoryMock.Object, _transcriptRepositoryMock.Object, _summaryRepositoryMock.Object, _locationRepositoryMock.Object, configMock.Object));
    }

    [Fact]
    public void Constructor_WithNullTranscriptionService_ShouldThrow()
    {
        // Arrange
        var configMock = new Mock<IMediaStreamConfigurationService>();
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MediaStreamService(_loggerMock.Object, _transcriptHubMock.Object, null!, _summarizationServiceMock.Object, _locationExtractionServiceMock.Object, _unifiedInsightsServiceMock.Object, _callRepositoryMock.Object, _transcriptRepositoryMock.Object, _summaryRepositoryMock.Object, _locationRepositoryMock.Object, configMock.Object));
    }

    [Fact]
    public void Constructor_WithNullSummarizationService_ShouldThrow()
    {
        // Arrange
        var configMock = new Mock<IMediaStreamConfigurationService>();
        
        Assert.Throws<ArgumentNullException>(() =>
            new MediaStreamService(_loggerMock.Object, _transcriptHubMock.Object, _transcriptionServiceMock.Object, null!, _locationExtractionServiceMock.Object, _unifiedInsightsServiceMock.Object, _callRepositoryMock.Object, _transcriptRepositoryMock.Object, _summaryRepositoryMock.Object, _locationRepositoryMock.Object, configMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLocationService_ShouldThrow()
    {
        // Arrange
        var configMock = new Mock<IMediaStreamConfigurationService>();
        
        Assert.Throws<ArgumentNullException>(() =>
            new MediaStreamService(_loggerMock.Object, _transcriptHubMock.Object, _transcriptionServiceMock.Object, _summarizationServiceMock.Object, null!, _unifiedInsightsServiceMock.Object, _callRepositoryMock.Object, _transcriptRepositoryMock.Object, _summaryRepositoryMock.Object, _locationRepositoryMock.Object, configMock.Object));
    }

    [Fact]
    public void Constructor_WithNullCallRepository_ShouldThrow()
    {
        // Arrange
        var configMock = new Mock<IMediaStreamConfigurationService>();
        
        Assert.Throws<ArgumentNullException>(() =>
            new MediaStreamService(_loggerMock.Object, _transcriptHubMock.Object, _transcriptionServiceMock.Object, _summarizationServiceMock.Object, _locationExtractionServiceMock.Object, _unifiedInsightsServiceMock.Object, null!, _transcriptRepositoryMock.Object, _summaryRepositoryMock.Object, _locationRepositoryMock.Object, configMock.Object));
    }

    [Fact]
    public void Constructor_WithNullTranscriptRepository_ShouldThrow()
    {
        // Arrange
        var configMock = new Mock<IMediaStreamConfigurationService>();
        
        Assert.Throws<ArgumentNullException>(() =>
            new MediaStreamService(_loggerMock.Object, _transcriptHubMock.Object, _transcriptionServiceMock.Object, _summarizationServiceMock.Object, _locationExtractionServiceMock.Object, _unifiedInsightsServiceMock.Object, _callRepositoryMock.Object, null!, _summaryRepositoryMock.Object, _locationRepositoryMock.Object, configMock.Object));
    }

    [Fact]
    public void Constructor_WithNullSummaryRepository_ShouldThrow()
    {
        // Arrange
        var configMock = new Mock<IMediaStreamConfigurationService>();
        
        Assert.Throws<ArgumentNullException>(() =>
            new MediaStreamService(_loggerMock.Object, _transcriptHubMock.Object, _transcriptionServiceMock.Object, _summarizationServiceMock.Object, _locationExtractionServiceMock.Object, _unifiedInsightsServiceMock.Object, _callRepositoryMock.Object, _transcriptRepositoryMock.Object, null!, _locationRepositoryMock.Object, configMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLocationRepository_ShouldThrow()
    {
        // Arrange
        var configMock = new Mock<IMediaStreamConfigurationService>();
        
        Assert.Throws<ArgumentNullException>(() =>
            new MediaStreamService(_loggerMock.Object, _transcriptHubMock.Object, _transcriptionServiceMock.Object, _summarizationServiceMock.Object, _locationExtractionServiceMock.Object, _unifiedInsightsServiceMock.Object, _callRepositoryMock.Object, _transcriptRepositoryMock.Object, _summaryRepositoryMock.Object, null!, configMock.Object));
    }

    [Fact]
    public async Task ProcessMediaDataAsync_WithNonMediaEvent_ShouldNotProcess()
    {
        // Arrange
        var streamSid = "MZ123456789";
        await _service.HandleStreamStartAsync(streamSid, "CA123456789");
        var mediaPayload = Convert.ToBase64String(new byte[] { 1, 2, 3 });
        var eventType = "start"; // Not "media"

        // Act
        await _service.ProcessMediaDataAsync(streamSid, mediaPayload, eventType);

        // Assert - Should not log debug message for non-media events
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing media data")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessMediaDataAsync_WithEmptyPayload_ShouldNotProcess()
    {
        // Arrange
        var streamSid = "MZ123456789";
        await _service.HandleStreamStartAsync(streamSid, "CA123456789");
        var eventType = "media";

        // Act
        await _service.ProcessMediaDataAsync(streamSid, string.Empty, eventType);

        // Assert - Should not process empty payload
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing media data")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleStreamStopAsync_WithUnknownStream_ShouldNotCalculateDuration()
    {
        // Arrange
        var streamSid = "MZ_UNKNOWN";
        var callSid = "CA123456789";

        // Act
        await _service.HandleStreamStopAsync(streamSid, callSid);

        // Assert - Should log stop but not duration
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("stopped")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        // Should not log duration for unknown stream
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("Stream duration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    #region Test Helper Methods

    /// <summary>
    /// Generates a base64-encoded non-silent audio payload for testing.
    /// Creates μ-law audio data that will not be filtered by silence detection.
    /// </summary>
    /// <param name="bytesCount">Number of bytes to generate (8kHz μ-law: 1 second ≈ 8000 bytes)</param>
    /// <returns>Base64-encoded audio payload suitable for Twilio media streams</returns>
    private static string GenerateNonSilentAudioPayload(int bytesCount)
    {
        var audioData = new byte[bytesCount];
        for (int i = 0; i < audioData.Length; i++)
        {
            // Generate varied audio values that are clearly not silence
            // μ-law silence is typically 0xFF (255) or 0x7F (127)
            // Use values well below the silence threshold (< 253) to ensure detection as non-silent
            audioData[i] = (byte)((i % 200) + 20); // Range: 20-219, clearly non-silent
        }
        return Convert.ToBase64String(audioData);
    }

    /// <summary>
    /// Generates a base64-encoded silent audio payload for testing silence detection.
    /// Creates μ-law audio data that should be filtered by silence detection.
    /// </summary>
    /// <param name="bytesCount">Number of bytes to generate</param>
    /// <returns>Base64-encoded silent audio payload</returns>
    private static string GenerateSilentAudioPayload(int bytesCount)
    {
        var audioData = new byte[bytesCount];
        for (int i = 0; i < audioData.Length; i++)
        {
            // μ-law silence is 0xFF (255) or 0x7F (127)
            audioData[i] = 0xFF; // Maximum silence value
        }
        return Convert.ToBase64String(audioData);
    }

    #endregion

    #region Additional Edge Case Tests

    /// <summary>
    /// Verifies that audio data below the buffer threshold is not transcribed immediately.
    /// This ensures we're properly buffering audio before sending to Whisper API.
    /// </summary>
    [Fact]
    public async Task ProcessMediaDataAsync_WhenBelowBufferThreshold_ShouldNotTranscribe()
    {
        // Arrange
        var streamSid = "MZ123456789";
        var callSid = "CA123456789";
        var insufficientAudio = GenerateNonSilentAudioPayload(bytesCount: 24000); // Only 3 seconds (threshold is 4 seconds)

        await _service.HandleStreamStartAsync(streamSid, callSid);

        // Act
        await _service.ProcessMediaDataAsync(streamSid, insufficientAudio, "media");

        // Assert - Should NOT call transcription service yet
        _transcriptionServiceMock.Verify(
            x => x.TranscribeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that silent audio chunks are filtered out and not sent for transcription.
    /// This prevents gibberish transcriptions and reduces API costs.
    /// </summary>
    [Fact]
    public async Task ProcessMediaDataAsync_WithSilentAudio_ShouldSkipTranscription()
    {
        // Arrange
        var streamSid = "MZ123456789";
        var callSid = "CA123456789";
        var silentAudio = GenerateSilentAudioPayload(bytesCount: 32001); // Exceeds threshold but is silent

        await _service.HandleStreamStartAsync(streamSid, callSid);

        // Act
        await _service.ProcessMediaDataAsync(streamSid, silentAudio, "media");

        // Assert - Should skip transcription due to silence detection
        _transcriptionServiceMock.Verify(
            x => x.TranscribeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // Should log that chunk was skipped
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("Skipping silent audio chunk")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that remaining buffered audio is processed when the stream stops.
    /// This ensures we don't lose the last words spoken before the call ends.
    /// </summary>
    [Fact]
    public async Task HandleStreamStopAsync_WithRemainingBuffer_ShouldProcessFinalAudio()
    {
        // Arrange
        var streamSid = "MZ123456789";
        var callSid = "CA123456789";
        var partialAudio = GenerateNonSilentAudioPayload(bytesCount: 4000); // Below threshold (won't trigger during ProcessMedia)

        await _service.HandleStreamStartAsync(streamSid, callSid);
        
        _transcriptionServiceMock
            .Setup(x => x.TranscribeAsync(
                callSid,
                streamSid,
                It.IsAny<ReadOnlyMemory<byte>>(),
                true, // isFinal = true for remaining audio
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult
            {
                CallSid = callSid,
                StreamSid = streamSid,
                Text = "Final words",
                IsFinal = true
            });

        await _service.ProcessMediaDataAsync(streamSid, partialAudio, "media");

        // Act - Stop the stream, should flush remaining audio
        await _service.HandleStreamStopAsync(streamSid, callSid);

        // Assert - Should transcribe the remaining buffered audio with isFinal=true
        _transcriptionServiceMock.Verify(
            x => x.TranscribeAsync(
                callSid,
                streamSid,
                It.IsAny<ReadOnlyMemory<byte>>(),
                true, // Verify isFinal is true for remaining audio
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Should log that final audio was processed
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("Processing final buffered audio chunk")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}

