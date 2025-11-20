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

public class OpenAiLocationExtractionServiceTests
{
    private readonly OpenAiOptions _options = new()
    {
        ApiKey = "test-key",
        Endpoint = "https://api.openai.com/v1/",
        LocationModel = "gpt-4o-mini"
    };

    [Fact]
    public async Task ExtractAsync_WithValidResponse_ShouldReturnLocation()
    {
        // Arrange
        var payload = new
        {
            choices = new[]
            {
                new {
                    message = new {
                        role = "assistant",
                        content = "{\"latitude\":37.0,\"longitude\":-122.0,\"address\":\"123 Main\",\"raw_location_text\":\"near 123 Main\",\"confidence\":0.85}"
                    }
                }
            }
        };

        var handler = SetupHandler(HttpStatusCode.OK, payload);
        var httpClient = new HttpClient(handler.Object);
        var logger = new Mock<ILogger<OpenAiLocationExtractionService>>();

        var service = new OpenAiLocationExtractionService(httpClient, Microsoft.Extensions.Options.Options.Create(_options), logger.Object);

        // Act
        var result = await service.ExtractAsync("CA123", "Caller at 123 Main St");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(37.0, result!.Latitude);
        Assert.Equal(-122.0, result.Longitude);

        // Verify we made TWO API calls: one for extraction, one for geocoding
        handler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
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

