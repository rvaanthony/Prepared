using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Prepared.Client.Hubs;
using Xunit;

namespace Prepared.Client.Tests.Hubs;

/// <summary>
/// Tests for TranscriptHub
/// Note: SignalR Hub testing is complex due to framework dependencies.
/// These tests focus on testable business logic and constructor validation.
/// Full integration testing would require TestHost or actual SignalR infrastructure.
/// </summary>
public class TranscriptHubTests
{
    private readonly Mock<ILogger<TranscriptHub>> _loggerMock;

    public TranscriptHubTests()
    {
        _loggerMock = new Mock<ILogger<TranscriptHub>>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new TranscriptHub(null!));
    }

    [Fact]
    public void Constructor_WithValidLogger_ShouldCreateInstance()
    {
        // Act
        var hub = new TranscriptHub(_loggerMock.Object);

        // Assert
        hub.Should().NotBeNull();
    }
}
