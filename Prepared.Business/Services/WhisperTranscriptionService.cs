using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Prepared.Business.Interfaces;
using Prepared.Common.Models;

namespace Prepared.Business.Services;

/// <summary>
/// Whisper (OpenAI) implementation of <see cref="ITranscriptionService"/>.
/// </summary>
public class WhisperTranscriptionService : ITranscriptionService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IWhisperConfigurationService _config;
    private readonly ILogger<WhisperTranscriptionService> _logger;

    public WhisperTranscriptionService(
        HttpClient httpClient,
        IWhisperConfigurationService config,
        ILogger<WhisperTranscriptionService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ConfigureHttpClient();
    }

    /// <summary>
    /// Transcribes audio data using OpenAI's Whisper API.
    /// </summary>
    /// <param name="callSid">The unique identifier for the call.</param>
    /// <param name="streamSid">The unique identifier for the media stream.</param>
    /// <param name="audioBytes">The audio data in mu-law PCM format.</param>
    /// <param name="isFinal">Whether this is the final audio chunk for the stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A transcription result, or null if transcription fails or audio is empty.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="callSid"/> or <paramref name="streamSid"/> is null or empty.</exception>
    public async Task<TranscriptionResult?> TranscribeAsync(
        string callSid,
        string streamSid,
        ReadOnlyMemory<byte> audioBytes,
        bool isFinal = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callSid))
            throw new ArgumentException("CallSid cannot be null or empty", nameof(callSid));
        if (string.IsNullOrWhiteSpace(streamSid))
            throw new ArgumentException("StreamSid cannot be null or empty", nameof(streamSid));
            
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
        if (string.IsNullOrWhiteSpace(_config.ApiKey))
        {
            _logger.LogWarning("Whisper API key is missing. Transcriptions will be skipped.");
            return;
        }

        _httpClient.BaseAddress = new Uri(_config.Endpoint.TrimEnd('/'));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
    }

    private MultipartFormDataContent BuildMultipartContent(ReadOnlyMemory<byte> audioBytes, bool isFinal)
    {
        var content = new MultipartFormDataContent();

        // Convert Twilio's mu-law PCM bytes into a proper WAV container that OpenAI accepts.
        var wavBytes = ConvertMuLawToWav(audioBytes.Span, sampleRate: 8000);

        var audioContent = new ByteArrayContent(wavBytes);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(audioContent, "file", $"chunk_{Guid.NewGuid():N}.wav");

        content.Add(new StringContent(_config.Model), "model");
        content.Add(new StringContent(_config.Temperature.ToString("0.0")), "temperature");

        return content;
    }

    /// <summary>
    /// Converts 8-bit mu-law PCM (as sent by Twilio Media Streams) into a 16-bit
    /// linear PCM WAV byte array.
    /// </summary>
    private static byte[] ConvertMuLawToWav(ReadOnlySpan<byte> muLawData, int sampleRate)
    {
        // Decode mu-law to 16-bit PCM
        var pcmSamples = new short[muLawData.Length];
        for (var i = 0; i < muLawData.Length; i++)
        {
            pcmSamples[i] = MuLawToPcm16(muLawData[i]);
        }

        // Build a standard 44-byte PCM WAV header
        const short audioFormat = 1; // PCM
        const short numChannels = 1;
        const short bitsPerSample = 16;
        var byteRate = sampleRate * numChannels * bitsPerSample / 8;
        var blockAlign = (short)(numChannels * bitsPerSample / 8);

        var dataSize = pcmSamples.Length * sizeof(short);
        var riffChunkSize = 36 + dataSize;

        using var ms = new MemoryStream(44 + dataSize);
        using var writer = new BinaryWriter(ms);

        // RIFF header
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(riffChunkSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        // fmt  subchunk
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // Subchunk1Size for PCM
        writer.Write(audioFormat);
        writer.Write(numChannels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);

        // data subchunk
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        // PCM data (little-endian)
        foreach (var sample in pcmSamples)
        {
            writer.Write(sample);
        }

        writer.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// Standard G.711 mu-law to 16-bit PCM conversion.
    /// </summary>
    private static short MuLawToPcm16(byte muLaw)
    {
        muLaw = (byte)~muLaw;

        var sign = muLaw & 0x80;
        var exponent = (muLaw & 0x70) >> 4;
        var mantissa = muLaw & 0x0F;
        var magnitude = ((mantissa << 3) + 0x84) << exponent;
        magnitude -= 0x84;

        return (short)(sign != 0 ? -magnitude : magnitude);
    }

    private sealed record WhisperResponse(string Text, double? Confidence);
}

