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

public class WhisperTranscriptionServiceTests
{
    private readonly WhisperOptions _options = new()
    {
        ApiKey = "test-api-key",
        Model = "whisper-1",
        Endpoint = "https://api.openai.com/v1/audio/transcriptions"
    };

    [Fact]
    public async Task TranscribeAsync_WithSuccessfulResponse_ShouldReturnResult()
    {
        // Arrange
        var responsePayload = new
        {
            text = "hello world",
            confidence = 0.93
        };

        var handlerMock = SetupHandler(HttpStatusCode.OK, responsePayload);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri(_options.Endpoint)
        };

        var loggerMock = new Mock<ILogger<WhisperTranscriptionService>>();

        var service = new WhisperTranscriptionService(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(_options),
            loggerMock.Object);

        // Act
        var result = await service.TranscribeAsync(
            "CA123",
            "MS123",
            new byte[] { 1, 2, 3, 4 });

        // Assert
        Assert.NotNull(result);
        Assert.Equal("hello world", result!.Text);
        Assert.False(result.IsFinal);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task TranscribeAsync_WithEmptyAudio_ShouldSkip()
    {
        // Arrange
        var handlerMock = SetupHandler(HttpStatusCode.OK, new { text = "ignored" });
        var httpClient = new HttpClient(handlerMock.Object);
        var loggerMock = new Mock<ILogger<WhisperTranscriptionService>>();

        var service = new WhisperTranscriptionService(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(_options),
            loggerMock.Object);

        // Act
        var result = await service.TranscribeAsync("CA123", "MS123", ReadOnlyMemory<byte>.Empty);

        // Assert
        Assert.Null(result);
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
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

