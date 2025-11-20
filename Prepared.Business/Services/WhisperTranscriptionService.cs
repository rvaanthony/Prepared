using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prepared.Business.Interfaces;
using Prepared.Business.Options;
using Prepared.Common.Models;

namespace Prepared.Business.Services;

/// <summary>
/// Whisper (OpenAI) implementation of <see cref="ITranscriptionService"/>.
/// </summary>
public class WhisperTranscriptionService : ITranscriptionService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly WhisperOptions _options;
    private readonly ILogger<WhisperTranscriptionService> _logger;

    public WhisperTranscriptionService(
        HttpClient httpClient,
        IOptions<WhisperOptions> options,
        ILogger<WhisperTranscriptionService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ConfigureHttpClient();
    }

    public async Task<TranscriptionResult?> TranscribeAsync(
        string callSid,
        string streamSid,
        ReadOnlyMemory<byte> audioBytes,
        bool isFinal = false,
        CancellationToken cancellationToken = default)
    {
        if (audioBytes.IsEmpty)
        {
            _logger.LogDebug("Skipping transcription (empty audio): CallSid={CallSid}, StreamSid={StreamSid}", callSid, streamSid);
            return null;
        }

        try
        {
            using var content = BuildMultipartContent(audioBytes, isFinal);

            using var response = await _httpClient.PostAsync(string.Empty, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Whisper transcription failed ({StatusCode}): {Message}", response.StatusCode, errorMessage);
                return null;
            }

            var whisperResponse = await response.Content.ReadFromJsonAsync<WhisperResponse>(SerializerOptions, cancellationToken);
            if (whisperResponse == null || string.IsNullOrWhiteSpace(whisperResponse.Text))
            {
                _logger.LogDebug("Whisper returned no text: CallSid={CallSid}, StreamSid={StreamSid}", callSid, streamSid);
                return null;
            }

            return new TranscriptionResult
            {
                CallSid = callSid,
                StreamSid = streamSid,
                Text = whisperResponse.Text.Trim(),
                IsFinal = isFinal,
                Confidence = whisperResponse.Confidence,
                TimestampUtc = DateTime.UtcNow
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Whisper transcription canceled: CallSid={CallSid}, StreamSid={StreamSid}", callSid, streamSid);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Whisper transcription error: CallSid={CallSid}, StreamSid={StreamSid}", callSid, streamSid);
            return null;
        }
    }

    private void ConfigureHttpClient()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("Whisper API key is missing. Transcriptions will be skipped.");
            return;
        }

        _httpClient.BaseAddress = new Uri(_options.Endpoint.TrimEnd('/'));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    private MultipartFormDataContent BuildMultipartContent(ReadOnlyMemory<byte> audioBytes, bool isFinal)
    {
        var content = new MultipartFormDataContent();

        // Add audio content as a stream (Whisper expects audio file upload)
        var audioContent = new ByteArrayContent(audioBytes.ToArray());
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(audioContent, "file", $"chunk_{Guid.NewGuid():N}.wav");

        content.Add(new StringContent(_options.Model), "model");
        content.Add(new StringContent(_options.Temperature.ToString("0.0")), "temperature");

        return content;
    }

    private sealed record WhisperResponse(string Text, double? Confidence);
}

