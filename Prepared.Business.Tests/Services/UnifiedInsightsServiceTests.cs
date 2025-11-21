using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Prepared.Business.Interfaces;
using Prepared.Business.Services;
using Xunit;

namespace Prepared.Business.Tests.Services;

public class UnifiedInsightsServiceTests
{
    private readonly Mock<IOpenAiConfigurationService> _configMock;

    public UnifiedInsightsServiceTests()
    {
        _configMock = new Mock<IOpenAiConfigurationService>();
        _configMock.Setup(x => x.ApiKey).Returns("test-key");
        _configMock.Setup(x => x.Endpoint).Returns("https://api.openai.com/v1/");
        _configMock.Setup(x => x.DefaultModel).Returns("gpt-4o-mini");
        _configMock.Setup(x => x.SummarizationModel).Returns("gpt-4o-mini");
        _configMock.Setup(x => x.LocationModel).Returns("gpt-4o-mini");
        _configMock.Setup(x => x.TimeoutSeconds).Returns(60);
    }

    [Fact]
    public async Task ExtractInsightsAsync_WithValidResponse_ShouldReturnInsights()
    {
        // Arrange
        var responsePayload = new
        {
            choices = new[]
            {
                new {
                    message = new {
                        role = "assistant",
                        content = "{\"location\":{\"address\":\"600 East Broad Street, Richmond, Virginia\",\"latitude\":37.5407,\"longitude\":-77.4360,\"confidence\":0.9},\"summary\":\"Test summary\",\"key_findings\":[\"Finding 1\",\"Finding 2\"]}"
                    }
                }
            }
        };

        var handler = SetupHandler(HttpStatusCode.OK, responsePayload);
        var httpClient = new HttpClient(handler.Object);
        var logger = new Mock<ILogger<UnifiedInsightsService>>();
        var service = new UnifiedInsightsService(httpClient, _configMock.Object, logger.Object);

        // Act
        var result = await service.ExtractInsightsAsync("CA123", "test transcript");

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result!.Location);
        Assert.NotNull(result.Summary);
        Assert.Equal("600 East Broad Street, Richmond, Virginia", result.Location.FormattedAddress);
        Assert.Equal(37.5407, result.Location.Latitude);
        Assert.Equal(-77.4360, result.Location.Longitude);
        Assert.Equal(2, result.Summary.KeyFindings.Count);

        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ExtractInsightsAsync_WithNullCallSid_ShouldThrow()
    {
        // Arrange
        var handler = SetupHandler(HttpStatusCode.OK, new { choices = Array.Empty<object>() });
        var httpClient = new HttpClient(handler.Object);
        var logger = new Mock<ILogger<UnifiedInsightsService>>();
        var service = new UnifiedInsightsService(httpClient, _configMock.Object, logger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExtractInsightsAsync(null!, "test transcript"));
    }

    [Fact]
    public async Task ExtractInsightsAsync_WithEmptyCallSid_ShouldThrow()
    {
        // Arrange
        var handler = SetupHandler(HttpStatusCode.OK, new { choices = Array.Empty<object>() });
        var httpClient = new HttpClient(handler.Object);
        var logger = new Mock<ILogger<UnifiedInsightsService>>();
        var service = new UnifiedInsightsService(httpClient, _configMock.Object, logger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExtractInsightsAsync(string.Empty, "test transcript"));
    }

    [Fact]
    public async Task ExtractInsightsAsync_WithEmptyTranscript_ShouldReturnNull()
    {
        // Arrange
        var handler = SetupHandler(HttpStatusCode.OK, new { choices = Array.Empty<object>() });
        var httpClient = new HttpClient(handler.Object);
        var logger = new Mock<ILogger<UnifiedInsightsService>>();
        var service = new UnifiedInsightsService(httpClient, _configMock.Object, logger.Object);

        // Act
        var result = await service.ExtractInsightsAsync("CA123", string.Empty);

        // Assert
        Assert.Null(result);
        handler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    private static Mock<HttpMessageHandler> SetupHandler(HttpStatusCode statusCode, object payload)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(statusCode)
                {
                    Content = JsonContent.Create(payload)
                };
                return response;
            });

        return handler;
    }
}

