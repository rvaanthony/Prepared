using FluentAssertions;
using Prepared.Common.Models;
using Xunit;

namespace Prepared.Common.Tests.Models;

public class MediaStreamInfoTests
{
    [Fact]
    public void MediaStreamInfo_ShouldInitializeWithDefaultValues()
    {
        // Act
        var mediaStreamInfo = new MediaStreamInfo();

        // Assert
        mediaStreamInfo.StreamSid.Should().BeEmpty();
        mediaStreamInfo.CallSid.Should().BeEmpty();
        mediaStreamInfo.Status.Should().BeEmpty();
        mediaStreamInfo.StartedAt.Should().Be(default(DateTime));
        mediaStreamInfo.StoppedAt.Should().BeNull();
    }

    [Fact]
    public void MediaStreamInfo_ShouldSetAndGetProperties()
    {
        // Arrange
        var mediaStreamInfo = new MediaStreamInfo();
        var streamSid = "MS123456789";
        var callSid = "CA123456789";
        var status = "started";
        var startedAt = DateTime.UtcNow;
        var stoppedAt = DateTime.UtcNow.AddMinutes(5);

        // Act
        mediaStreamInfo.StreamSid = streamSid;
        mediaStreamInfo.CallSid = callSid;
        mediaStreamInfo.Status = status;
        mediaStreamInfo.StartedAt = startedAt;
        mediaStreamInfo.StoppedAt = stoppedAt;

        // Assert
        mediaStreamInfo.StreamSid.Should().Be(streamSid);
        mediaStreamInfo.CallSid.Should().Be(callSid);
        mediaStreamInfo.Status.Should().Be(status);
        mediaStreamInfo.StartedAt.Should().Be(startedAt);
        mediaStreamInfo.StoppedAt.Should().Be(stoppedAt);
    }

    [Fact]
    public void MediaStreamInfo_StoppedAt_ShouldBeNullable()
    {
        // Arrange
        var mediaStreamInfo = new MediaStreamInfo
        {
            StoppedAt = DateTime.UtcNow
        };

        // Act
        mediaStreamInfo.StoppedAt = null;

        // Assert
        mediaStreamInfo.StoppedAt.Should().BeNull();
    }

    [Fact]
    public void MediaStreamInfo_ShouldHandleEmptyStrings()
    {
        // Arrange
        var mediaStreamInfo = new MediaStreamInfo
        {
            StreamSid = "MS123",
            CallSid = "CA123",
            Status = "started"
        };

        // Act
        mediaStreamInfo.StreamSid = string.Empty;
        mediaStreamInfo.CallSid = string.Empty;
        mediaStreamInfo.Status = string.Empty;

        // Assert
        mediaStreamInfo.StreamSid.Should().BeEmpty();
        mediaStreamInfo.CallSid.Should().BeEmpty();
        mediaStreamInfo.Status.Should().BeEmpty();
    }
}

