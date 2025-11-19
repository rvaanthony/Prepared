using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Prepared.Business.Interfaces;
using Prepared.Client.APIs;

namespace Prepared.Client.Tests.APIs;

public class MediaStreamControllerTests
{
    private readonly Mock<IMediaStreamService> _mediaStreamServiceMock;
    private readonly Mock<ILogger<MediaStreamController>> _loggerMock;
    private readonly MediaStreamController _controller;

    public MediaStreamControllerTests()
    {
        _mediaStreamServiceMock = new Mock<IMediaStreamService>();
        _loggerMock = new Mock<ILogger<MediaStreamController>>();
        _controller = new MediaStreamController(_mediaStreamServiceMock.Object, _loggerMock.Object);
        
        SetupControllerContext();
    }

    private void SetupControllerContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "StreamSid", "MZ123456789" },
            { "CallSid", "CA123456789" },
            { "Event", "start" },
            { "MediaPayload", "" }
        });

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task HandleMediaStream_WithStartEvent_ShouldCallHandleStreamStart()
    {
        // Arrange
        _controller.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "StreamSid", "MZ123456789" },
            { "CallSid", "CA123456789" },
            { "Event", "start" }
        });

        // Act
        var result = await _controller.HandleMediaStream();

        // Assert
        result.Should().BeOfType<OkResult>();
        
        _mediaStreamServiceMock.Verify(
            x => x.HandleStreamStartAsync("MZ123456789", "CA123456789", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleMediaStream_WithMediaEvent_ShouldCallProcessMediaData()
    {
        // Arrange
        var mediaPayload = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 });
        _controller.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "StreamSid", "MZ123456789" },
            { "CallSid", "CA123456789" },
            { "Event", "media" },
            { "MediaPayload", mediaPayload }
        });

        // Act
        var result = await _controller.HandleMediaStream();

        // Assert
        result.Should().BeOfType<OkResult>();
        
        _mediaStreamServiceMock.Verify(
            x => x.ProcessMediaDataAsync("MZ123456789", mediaPayload, "media", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleMediaStream_WithStopEvent_ShouldCallHandleStreamStop()
    {
        // Arrange
        _controller.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "StreamSid", "MZ123456789" },
            { "CallSid", "CA123456789" },
            { "Event", "stop" }
        });

        // Act
        var result = await _controller.HandleMediaStream();

        // Assert
        result.Should().BeOfType<OkResult>();
        
        _mediaStreamServiceMock.Verify(
            x => x.HandleStreamStopAsync("MZ123456789", "CA123456789", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleMediaStream_WithUnknownEvent_ShouldLogWarning()
    {
        // Arrange
        _controller.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "StreamSid", "MZ123456789" },
            { "CallSid", "CA123456789" },
            { "Event", "unknown-event" }
        });

        // Act
        var result = await _controller.HandleMediaStream();

        // Assert
        result.Should().BeOfType<OkResult>();
        
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("Unknown media stream event")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleMediaStream_OnException_ShouldReturnOk()
    {
        // Arrange
        _controller.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "StreamSid", "MZ123456789" },
            { "CallSid", "CA123456789" },
            { "Event", "start" }
        });
        
        _mediaStreamServiceMock
            .Setup(x => x.HandleStreamStartAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.HandleMediaStream();

        // Assert
        // Should return 200 OK even on error to prevent Twilio retries
        result.Should().BeOfType<OkResult>();
        
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleMediaStream_WithCaseInsensitiveEvent_ShouldHandleCorrectly()
    {
        // Arrange
        _controller.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "StreamSid", "MZ123456789" },
            { "CallSid", "CA123456789" },
            { "Event", "START" } // Uppercase
        });

        // Act
        var result = await _controller.HandleMediaStream();

        // Assert
        result.Should().BeOfType<OkResult>();
        
        _mediaStreamServiceMock.Verify(
            x => x.HandleStreamStartAsync("MZ123456789", "CA123456789", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

