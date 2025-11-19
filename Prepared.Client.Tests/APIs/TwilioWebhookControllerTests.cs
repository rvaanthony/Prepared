using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Prepared.Business.Interfaces;
using Prepared.Client.APIs;
using Prepared.Common.Models;
using System.Text;
using Twilio.AspNet.Core;

namespace Prepared.Client.Tests.APIs;

public class TwilioWebhookControllerTests
{
    private readonly Mock<ITwilioService> _twilioServiceMock;
    private readonly Mock<ILogger<TwilioWebhookController>> _loggerMock;
    private readonly TwilioWebhookController _controller;

    public TwilioWebhookControllerTests()
    {
        _twilioServiceMock = new Mock<ITwilioService>();
        _loggerMock = new Mock<ILogger<TwilioWebhookController>>();
        _controller = new TwilioWebhookController(_twilioServiceMock.Object, _loggerMock.Object);
        
        SetupControllerContext();
    }

    private void SetupControllerContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("example.com");
        httpContext.Request.Path = "/api/twilio/incoming-call";
        httpContext.Request.Method = "POST";
        httpContext.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "CallSid", "CA123456789" },
            { "From", "+1234567890" },
            { "To", "+0987654321" },
            { "Direction", "inbound" },
            { "CallStatus", "in-progress" },
            { "AccountSid", "AC123456789" }
        });

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task HandleIncomingCall_WithValidSignature_ShouldReturnTwiML()
    {
        // Arrange
        _controller.Request.Headers["X-Twilio-Signature"] = "valid-signature";
        _twilioServiceMock
            .Setup(x => x.ValidateWebhookSignature(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), "valid-signature"))
            .Returns(true);
        _twilioServiceMock
            .Setup(x => x.HandleIncomingCallAsync(It.IsAny<CallInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Response></Response>");

        // Act
        var result = await _controller.HandleIncomingCall();

        // Assert
        result.Should().BeOfType<ContentResult>();
        var contentResult = result as ContentResult;
        contentResult!.Content.Should().Contain("<?xml");
        contentResult.ContentType.Should().Be("application/xml");
        
        _twilioServiceMock.Verify(
            x => x.ValidateWebhookSignature(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), "valid-signature"),
            Times.Once);
        _twilioServiceMock.Verify(
            x => x.HandleIncomingCallAsync(It.Is<CallInfo>(c => c.CallSid == "CA123456789"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleIncomingCall_WithoutSignature_ShouldReturnUnauthorized()
    {
        // Arrange
        _controller.Request.Headers.Remove("X-Twilio-Signature");

        // Act
        var result = await _controller.HandleIncomingCall();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
        
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("missing signature")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleIncomingCall_WithInvalidSignature_ShouldReturnUnauthorized()
    {
        // Arrange
        _controller.Request.Headers["X-Twilio-Signature"] = "invalid-signature";
        _twilioServiceMock
            .Setup(x => x.ValidateWebhookSignature(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), "invalid-signature"))
            .Returns(false);

        // Act
        var result = await _controller.HandleIncomingCall();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
        
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid webhook signature")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleIncomingCall_OnException_ShouldReturnErrorTwiML()
    {
        // Arrange
        _controller.Request.Headers["X-Twilio-Signature"] = "valid-signature";
        _twilioServiceMock
            .Setup(x => x.ValidateWebhookSignature(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), "valid-signature"))
            .Returns(true);
        _twilioServiceMock
            .Setup(x => x.HandleIncomingCallAsync(It.IsAny<CallInfo>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.HandleIncomingCall();

        // Assert
        result.Should().BeOfType<ContentResult>();
        var contentResult = result as ContentResult;
        contentResult!.Content.Should().Contain("<?xml");
        contentResult.Content.Should().Contain("<Hangup>");
        contentResult.ContentType.Should().Be("application/xml");
        
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
    public async Task HandleCallStatus_WithValidSignature_ShouldReturnOk()
    {
        // Arrange
        _controller.Request.Headers["X-Twilio-Signature"] = "valid-signature";
        _controller.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "CallSid", "CA123456789" },
            { "CallStatus", "completed" }
        });
        _controller.Request.Path = "/api/twilio/call-status";
        
        _twilioServiceMock
            .Setup(x => x.ValidateWebhookSignature(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), "valid-signature"))
            .Returns(true);
        _twilioServiceMock
            .Setup(x => x.HandleCallStatusUpdateAsync("CA123456789", "completed", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.HandleCallStatus();

        // Assert
        result.Should().BeOfType<OkResult>();
        
        _twilioServiceMock.Verify(
            x => x.HandleCallStatusUpdateAsync("CA123456789", "completed", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleCallStatus_WithoutSignature_ShouldReturnUnauthorized()
    {
        // Arrange
        _controller.Request.Headers.Remove("X-Twilio-Signature");
        _controller.Request.Path = "/api/twilio/call-status";

        // Act
        var result = await _controller.HandleCallStatus();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task HandleCallStatus_OnException_ShouldReturn500()
    {
        // Arrange
        _controller.Request.Headers["X-Twilio-Signature"] = "valid-signature";
        _controller.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "CallSid", "CA123456789" },
            { "CallStatus", "completed" }
        });
        _controller.Request.Path = "/api/twilio/call-status";
        
        _twilioServiceMock
            .Setup(x => x.ValidateWebhookSignature(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), "valid-signature"))
            .Returns(true);
        _twilioServiceMock
            .Setup(x => x.HandleCallStatusUpdateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.HandleCallStatus();

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
        
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

