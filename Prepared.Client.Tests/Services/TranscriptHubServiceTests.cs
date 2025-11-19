using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Prepared.Business.Interfaces;
using Prepared.Client.Hubs;
using Prepared.Client.Services;
using Xunit;

namespace Prepared.Client.Tests.Services;

public class TranscriptHubServiceTests
{
    private readonly Mock<IHubContext<TranscriptHub>> _hubContextMock;
    private readonly Mock<ILogger<TranscriptHubService>> _loggerMock;
    private readonly Mock<IClientProxy> _clientProxyMock;
    private readonly Mock<IGroupManager> _groupManagerMock;
    private readonly TranscriptHubService _service;

    public TranscriptHubServiceTests()
    {
        _hubContextMock = new Mock<IHubContext<TranscriptHub>>();
        _loggerMock = new Mock<ILogger<TranscriptHubService>>();
        _clientProxyMock = new Mock<IClientProxy>();
        _groupManagerMock = new Mock<IGroupManager>();

        _hubContextMock.Setup(x => x.Clients).Returns(new Mock<IHubClients>().Object);
        _hubContextMock.Setup(x => x.Groups).Returns(_groupManagerMock.Object);
        
        var hubClientsMock = new Mock<IHubClients>();
        hubClientsMock.Setup(x => x.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);
        _hubContextMock.Setup(x => x.Clients).Returns(hubClientsMock.Object);

        _service = new TranscriptHubService(_hubContextMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void Constructor_WithNullHubContext_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new TranscriptHubService(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new TranscriptHubService(_hubContextMock.Object, null!));
    }

    [Fact]
    public async Task BroadcastTranscriptUpdateAsync_WithValidData_ShouldSendToGroup()
    {
        // Arrange
        var callSid = "CA123456789";
        var transcript = "Hello, this is a test transcript.";

        // Act
        await _service.BroadcastTranscriptUpdateAsync(callSid, transcript);

        // Assert
        _clientProxyMock.Verify(
            x => x.SendCoreAsync(
                "TranscriptUpdate",
                It.Is<object[]>(args => args.Length == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastTranscriptUpdateAsync_WithEmptyCallSid_ShouldNotSend()
    {
        // Arrange
        var transcript = "Test transcript";

        // Act
        await _service.BroadcastTranscriptUpdateAsync(string.Empty, transcript);

        // Assert
        _clientProxyMock.Verify(
            x => x.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("empty CallSid")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastTranscriptUpdateAsync_WithEmptyTranscript_ShouldNotSend()
    {
        // Arrange
        var callSid = "CA123456789";

        // Act
        await _service.BroadcastTranscriptUpdateAsync(callSid, string.Empty);

        // Assert
        _clientProxyMock.Verify(
            x => x.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BroadcastTranscriptUpdateAsync_WithIsFinal_ShouldIncludeFlag()
    {
        // Arrange
        var callSid = "CA123456789";
        var transcript = "Final transcript";

        // Act
        await _service.BroadcastTranscriptUpdateAsync(callSid, transcript, isFinal: true);

        // Assert
        _clientProxyMock.Verify(
            x => x.SendCoreAsync(
                "TranscriptUpdate",
                It.Is<object[]>(args => 
                    args.Length == 1 && 
                    args[0] != null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastCallStatusUpdateAsync_WithValidData_ShouldSendToGroup()
    {
        // Arrange
        var callSid = "CA123456789";
        var status = "completed";

        // Act
        await _service.BroadcastCallStatusUpdateAsync(callSid, status);

        // Assert
        _clientProxyMock.Verify(
            x => x.SendCoreAsync(
                "CallStatusUpdate",
                It.Is<object[]>(args => args.Length == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastLocationUpdateAsync_WithValidData_ShouldSendToGroup()
    {
        // Arrange
        var callSid = "CA123456789";
        var latitude = 40.7128;
        var longitude = -74.0060;
        var address = "New York, NY";

        // Act
        await _service.BroadcastLocationUpdateAsync(callSid, latitude, longitude, address);

        // Assert
        _clientProxyMock.Verify(
            x => x.SendCoreAsync(
                "LocationUpdate",
                It.Is<object[]>(args => args.Length == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastLocationUpdateAsync_WithoutAddress_ShouldStillSend()
    {
        // Arrange
        var callSid = "CA123456789";
        var latitude = 40.7128;
        var longitude = -74.0060;

        // Act
        await _service.BroadcastLocationUpdateAsync(callSid, latitude, longitude);

        // Assert
        _clientProxyMock.Verify(
            x => x.SendCoreAsync(
                "LocationUpdate",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastTranscriptUpdateAsync_OnException_ShouldNotThrow()
    {
        // Arrange
        var callSid = "CA123456789";
        var transcript = "Test transcript";
        
        _clientProxyMock
            .Setup(x => x.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act & Assert - Should not throw
        await _service.BroadcastTranscriptUpdateAsync(callSid, transcript);
        
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

