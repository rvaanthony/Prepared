using FluentAssertions;
using Microsoft.Extensions.Options;
using Prepared.Business.Options;
using Prepared.Business.Services;
using Xunit;
using Options = Microsoft.Extensions.Options.Options;

namespace Prepared.Business.Tests.Services;

public class MediaStreamConfigurationServiceTests
{
    [Fact]
    public void Properties_ShouldReturnValuesFromOptions()
    {
        // Arrange
        var options = new MediaStreamOptions
        {
            AudioBufferSeconds = 5.0,
            SilenceThreshold = 0.85,
            SampleRate = 16000
        };
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(options);
        var service = new MediaStreamConfigurationService(optionsWrapper);

        // Act & Assert
        service.AudioBufferSeconds.Should().Be(5.0);
        service.SilenceThreshold.Should().Be(0.85);
        service.SampleRate.Should().Be(16000);
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrow()
    {
        // Arrange
        IOptions<MediaStreamOptions>? options = null;

        // Act & Assert
        Assert.Throws<NullReferenceException>(() =>
            new MediaStreamConfigurationService(options!));
    }
}

