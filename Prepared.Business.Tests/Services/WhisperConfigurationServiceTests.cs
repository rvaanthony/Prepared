using FluentAssertions;
using Microsoft.Extensions.Options;
using Prepared.Business.Options;
using Prepared.Business.Services;
using Xunit;
using Options = Microsoft.Extensions.Options.Options;

namespace Prepared.Business.Tests.Services;

public class WhisperConfigurationServiceTests
{
    [Fact]
    public void Properties_ShouldReturnValuesFromOptions()
    {
        // Arrange
        var options = new WhisperOptions
        {
            ApiKey = "test-api-key",
            Model = "whisper-1",
            Endpoint = "https://api.openai.com/v1/audio/transcriptions",
            Temperature = 0.2,
            TimeoutSeconds = 120
        };
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(options);
        var service = new WhisperConfigurationService(optionsWrapper);

        // Act & Assert
        service.ApiKey.Should().Be("test-api-key");
        service.Model.Should().Be("whisper-1");
        service.Endpoint.Should().Be("https://api.openai.com/v1/audio/transcriptions");
        service.Temperature.Should().Be(0.2);
        service.TimeoutSeconds.Should().Be(120);
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrow()
    {
        // Arrange
        IOptions<WhisperOptions>? options = null;

        // Act & Assert
        Assert.Throws<NullReferenceException>(() =>
            new WhisperConfigurationService(options!));
    }
}

