using FluentAssertions;
using Microsoft.Extensions.Options;
using Prepared.Business.Options;
using Prepared.Business.Services;
using Xunit;
using Options = Microsoft.Extensions.Options.Options;

namespace Prepared.Business.Tests.Services;

public class OpenAiConfigurationServiceTests
{
    [Fact]
    public void Properties_ShouldReturnValuesFromOptions()
    {
        // Arrange
        var options = new OpenAiOptions
        {
            ApiKey = "test-api-key",
            Endpoint = "https://api.openai.com/v1/",
            DefaultModel = "gpt-4o-mini",
            SummarizationModel = "gpt-4o",
            LocationModel = "gpt-4o-mini",
            TimeoutSeconds = 90
        };
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(options);
        var service = new OpenAiConfigurationService(optionsWrapper);

        // Act & Assert
        service.ApiKey.Should().Be("test-api-key");
        service.Endpoint.Should().Be("https://api.openai.com/v1/");
        service.DefaultModel.Should().Be("gpt-4o-mini");
        service.SummarizationModel.Should().Be("gpt-4o");
        service.LocationModel.Should().Be("gpt-4o-mini");
        service.TimeoutSeconds.Should().Be(90);
    }

    [Fact]
    public void SummarizationModel_WhenNotSet_ShouldFallbackToDefaultModel()
    {
        // Arrange
        var options = new OpenAiOptions
        {
            ApiKey = "test-key",
            Endpoint = "https://api.openai.com/v1/",
            DefaultModel = "gpt-4o-mini",
            SummarizationModel = null, // Not set
            LocationModel = null,
            TimeoutSeconds = 60
        };
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(options);
        var service = new OpenAiConfigurationService(optionsWrapper);

        // Act & Assert
        service.SummarizationModel.Should().Be("gpt-4o-mini"); // Should fallback to DefaultModel
    }

    [Fact]
    public void LocationModel_WhenNotSet_ShouldFallbackToDefaultModel()
    {
        // Arrange
        var options = new OpenAiOptions
        {
            ApiKey = "test-key",
            Endpoint = "https://api.openai.com/v1/",
            DefaultModel = "gpt-4o-mini",
            SummarizationModel = null,
            LocationModel = null, // Not set
            TimeoutSeconds = 60
        };
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(options);
        var service = new OpenAiConfigurationService(optionsWrapper);

        // Act & Assert
        service.LocationModel.Should().Be("gpt-4o-mini"); // Should fallback to DefaultModel
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrow()
    {
        // Arrange
        IOptions<OpenAiOptions>? options = null;

        // Act & Assert
        Assert.Throws<NullReferenceException>(() =>
            new OpenAiConfigurationService(options!));
    }
}

