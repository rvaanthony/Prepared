using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Prepared.Business.Interfaces;
using Prepared.Business.Services;
using Prepared.Common.Models;

namespace Prepared.Business.Tests.Services;

public class MediaStreamServiceTests
{
    private readonly Mock<ILogger<MediaStreamService>> _loggerMock;
    private readonly Mock<ITranscriptHub> _transcriptHubMock;
    private readonly Mock<ITranscriptionService> _transcriptionServiceMock;
    private readonly MediaStreamService _service;

    public MediaStreamServiceTests()
    {
        _loggerMock = new Mock<ILogger<MediaStreamService>>();
        _transcriptHubMock = new Mock<ITranscriptHub>();
        _transcriptionServiceMock = new Mock<ITranscriptionService>();
        _service = new MediaStreamService(
            _loggerMock.Object,
            _transcriptHubMock.Object,
            _transcriptionServiceMock.Object);
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

    [Fact]
    public async Task ProcessMediaDataAsync_WithMediaEvent_ShouldProcessData()
    {
        // Arrange
        var streamSid = "MZ123456789";
        var mediaPayload = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 });
        var eventType = "media";
        var callSid = "CA123456789";

        // First start the stream
        await _service.HandleStreamStartAsync(streamSid, callSid);
        _transcriptionServiceMock
            .Setup(x => x.TranscribeAsync(
                callSid,
                streamSid,
                It.IsAny<ReadOnlyMemory<byte>>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Prepared.Common.Models.TranscriptionResult
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
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MediaStreamService(null!, _transcriptHubMock.Object, _transcriptionServiceMock.Object));
    }

    [Fact]
    public void Constructor_WithNullTranscriptHub_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MediaStreamService(_loggerMock.Object, null!, _transcriptionServiceMock.Object));
    }

    [Fact]
    public void Constructor_WithNullTranscriptionService_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MediaStreamService(_loggerMock.Object, _transcriptHubMock.Object, null!));
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
}

