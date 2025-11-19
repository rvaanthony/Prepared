using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prepared.Business.Interfaces;
using Prepared.Business.Options;
using Prepared.Common.Models;

namespace Prepared.Business.Services;

public class OpenAiLocationExtractionService : ILocationExtractionService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiLocationExtractionService> _logger;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public OpenAiLocationExtractionService(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiLocationExtractionService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ConfigureClient();
    }

    public async Task<LocationExtractionResult?> ExtractAsync(string callSid, string transcript, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return null;
        }

        try
        {
            var payload = new
            {
                model = _options.LocationModel ?? _options.DefaultModel,
                temperature = 0.1,
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new { role = "system", content = "Extract the best guess of the incident location from the transcript. Return JSON with latitude, longitude, address, confidence, raw_location_text." },
                    new { role = "user", content = transcript }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("chat/completions", payload, SerializerOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("OpenAI location extraction failed ({StatusCode}): {Message}", response.StatusCode, message);
                return null;
            }

            var completion = await response.Content.ReadFromJsonAsync<LocationCompletionResponse>(SerializerOptions, cancellationToken);
            var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            var parsed = JsonSerializer.Deserialize<LocationExtractionPayload>(content, SerializerOptions);
            if (parsed == null)
            {
                return null;
            }

            return new LocationExtractionResult
            {
                CallSid = callSid,
                RawLocationText = parsed.Raw_Location_Text,
                Latitude = parsed.Latitude,
                Longitude = parsed.Longitude,
                FormattedAddress = parsed.Address,
                Confidence = parsed.Confidence
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI location extraction error: CallSid={CallSid}", callSid);
            return null;
        }
    }

    private void ConfigureClient()
    {
        _httpClient.BaseAddress = new Uri(_options.Endpoint.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    private sealed record LocationCompletionResponse(ChatChoice[] Choices);
    private sealed record ChatChoice(ChatMessage Message);
    private sealed record ChatMessage(string Role, string Content);

    private sealed record LocationExtractionPayload(
        double? Latitude,
        double? Longitude,
        string? Address,
        string? Raw_Location_Text,
        double? Confidence);
}

