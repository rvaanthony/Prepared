using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Prepared.Business.Options;
using Prepared.Business.Services;
using Xunit;

namespace Prepared.Business.Tests.Services;

public class OpenAiSummarizationServiceTests
{
    private readonly OpenAiOptions _options = new()
    {
        ApiKey = "test-key",
        Endpoint = "https://api.openai.com/v1/",
        SummarizationModel = "gpt-4o-mini"
    };

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

        var service = new OpenAiSummarizationService(httpClient, Microsoft.Extensions.Options.Options.Create(_options), logger.Object);

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

