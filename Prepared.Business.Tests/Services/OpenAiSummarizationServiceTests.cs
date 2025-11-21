using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Prepared.Business.Interfaces;
using Prepared.Business.Services;
using Xunit;

namespace Prepared.Business.Tests.Services;

public class OpenAiSummarizationServiceTests
{
    private readonly Mock<IOpenAiConfigurationService> _configMock;

    public OpenAiSummarizationServiceTests()
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
    public async Task SummarizeAsync_WithResponse_ShouldReturnSummary()
    {
        // Arrange
        var responsePayload = new
        {
            choices = new[]
            {
                new {
                    message = new {
                        role = "assistant",
                        content = "- summary line\n- second"
                    }
                }
            }
        };

        var handler = SetupHandler(HttpStatusCode.OK, responsePayload);
        var httpClient = new HttpClient(handler.Object);
        var logger = new Mock<ILogger<OpenAiSummarizationService>>();

        var service = new OpenAiSummarizationService(httpClient, _configMock.Object, logger.Object);

        // Act
        var result = await service.SummarizeAsync("CA123", "test transcript");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("CA123", result!.CallSid);
        Assert.Contains("summary line", result.Summary);

        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SummarizeAsync_WithNullCallSid_ShouldThrow()
    {
        // Arrange
        var handler = SetupHandler(HttpStatusCode.OK, new { choices = Array.Empty<object>() });
        var httpClient = new HttpClient(handler.Object);
        var logger = new Mock<ILogger<OpenAiSummarizationService>>();
        var service = new OpenAiSummarizationService(httpClient, _configMock.Object, logger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SummarizeAsync(null!, "test transcript"));
    }

    [Fact]
    public async Task SummarizeAsync_WithEmptyCallSid_ShouldThrow()
    {
        // Arrange
        var handler = SetupHandler(HttpStatusCode.OK, new { choices = Array.Empty<object>() });
        var httpClient = new HttpClient(handler.Object);
        var logger = new Mock<ILogger<OpenAiSummarizationService>>();
        var service = new OpenAiSummarizationService(httpClient, _configMock.Object, logger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SummarizeAsync(string.Empty, "test transcript"));
    }

    private static Mock<HttpMessageHandler> SetupHandler(HttpStatusCode statusCode, object payload)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(statusCode);
                response.Content = JsonContent.Create(payload);
                return response;
            });

        return handlerMock;
    }
}

