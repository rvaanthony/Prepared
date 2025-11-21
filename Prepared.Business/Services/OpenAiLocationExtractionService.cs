using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Prepared.Business.Interfaces;
using Prepared.Common.Models;

namespace Prepared.Business.Services;

/// <summary>
/// OpenAI-powered location extraction service that identifies and geocodes locations from call transcripts.
/// Uses GPT models with JSON mode to extract structured location data including coordinates and addresses.
/// </summary>
public class OpenAiLocationExtractionService : ILocationExtractionService
{
    private readonly HttpClient _httpClient;
    private readonly IOpenAiConfigurationService _config;
    private readonly ILogger<OpenAiLocationExtractionService> _logger;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public OpenAiLocationExtractionService(
        HttpClient httpClient,
        IOpenAiConfigurationService config,
        ILogger<OpenAiLocationExtractionService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ConfigureClient();
    }

    /// <summary>
    /// Extracts location information from a call transcript using OpenAI.
    /// </summary>
    /// <param name="callSid">The unique identifier for the call.</param>
    /// <param name="transcript">The transcript text to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A location extraction result with address and coordinates, or null if no location found or extraction fails.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="callSid"/> is null or empty.</exception>
    public async Task<LocationExtractionResult?> ExtractAsync(string callSid, string transcript, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callSid))
            throw new ArgumentException("CallSid cannot be null or empty", nameof(callSid));
            
        if (string.IsNullOrWhiteSpace(transcript))
        {
            _logger.LogDebug("Skipping location extraction - transcript is empty: CallSid={CallSid}", callSid);
            return null;
        }

        try
        {
            // Step 1: Extract location text from transcript using GPT
            var systemPrompt = @"You are a location extraction AI for emergency dispatch. Extract location information from 911 call transcripts.

INSTRUCTIONS:
1. Find ANY mention of locations: street addresses, intersections, landmarks, business names, apartment numbers
2. Extract the COMPLETE address with all details (street number, street name, city, state, zip if mentioned)
3. Include nearby landmarks or cross streets if mentioned
4. If no specific location is mentioned, return null values
5. Return a confidence score (0.0 to 1.0)

EXAMPLES:
- ""I'm at 600 East Broad Street in Richmond, Virginia"" → address: ""600 East Broad Street, Richmond, Virginia"", confidence: 0.9
- ""Corner of Main and 5th"" → address: ""Main Street and 5th Street"", confidence: 0.7
- ""At the Starbucks on Market Street"" → address: ""Starbucks, Market Street"", confidence: 0.6

Return JSON with this EXACT structure:
{
  ""address"": ""full address or location description"",
  ""raw_location_text"": ""exact text from transcript mentioning location"",
  ""confidence"": 0.9
}

If NO location found, return:
{
  ""address"": null,
  ""raw_location_text"": null,
  ""confidence"": 0.0
}";

            var model = _config.LocationModel;
            var isGpt5Model = model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase);
            
            object payload;
            if (isGpt5Model)
            {
                payload = new
                {
                    model,
                    response_format = new { type = "json_object" },
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = $"Extract location from this transcript:\n\n{transcript}" }
                    }
                };
            }
            else
            {
                payload = new
                {
                    model,
                    temperature = 0.1, // Low temperature for consistent extraction (not supported by gpt-5 models)
                    response_format = new { type = "json_object" },
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = $"Extract location from this transcript:\n\n{transcript}" }
                    }
                };
            }

            var response = await _httpClient.PostAsJsonAsync("chat/completions", payload, SerializerOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "OpenAI location extraction failed ({StatusCode}): {Message}",
                    response.StatusCode, message);
                return null;
            }

            var completion = await response.Content.ReadFromJsonAsync<LocationCompletionResponse>(SerializerOptions, cancellationToken);
            var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogDebug("OpenAI returned empty content for location extraction: CallSid={CallSid}", callSid);
                return null;
            }

            _logger.LogInformation(
                "OpenAI location extraction response: CallSid={CallSid}, Content={Content}",
                callSid, content);

            var parsed = JsonSerializer.Deserialize<LocationExtractionPayload>(content, SerializerOptions);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.Address))
            {
                _logger.LogDebug(
                    "No location found in transcript: CallSid={CallSid}, Confidence={Confidence}",
                    callSid, parsed?.Confidence ?? 0);
                return null;
            }

            _logger.LogInformation(
                "Extracted address from transcript: CallSid={CallSid}, Address={Address}, Confidence={Confidence}",
                callSid, parsed.Address, parsed.Confidence);

            // Step 2: Geocode the extracted address using OpenAI's knowledge
            // Note: For production, consider using a dedicated geocoding API (Google Maps, Azure Maps)
            // For now, we'll ask GPT to provide approximate coordinates
            var geocodeResult = await GeocodeAddressAsync(parsed.Address, cancellationToken);
            
            return new LocationExtractionResult
            {
                CallSid = callSid,
                RawLocationText = parsed.Raw_Location_Text,
                Latitude = geocodeResult?.Latitude,
                Longitude = geocodeResult?.Longitude,
                FormattedAddress = parsed.Address,
                Confidence = parsed.Confidence ?? 0.0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI location extraction error: CallSid={CallSid}", callSid);
            return null;
        }
    }

    /// <summary>
    /// Geocode an address to latitude/longitude coordinates using GPT.
    /// Note: For production use, consider a dedicated geocoding API for better accuracy.
    /// </summary>
    private async Task<GeocodeResult?> GeocodeAddressAsync(string address, CancellationToken cancellationToken)
    {
        try
        {
            var geocodePrompt = $@"Provide the latitude and longitude coordinates for this address: ""{address}""

Return ONLY a JSON object with this exact structure (no other text):
{{
  ""latitude"": 37.1234,
  ""longitude"": -122.5678
}}

Use your best knowledge of real-world locations. If the address is incomplete, make your best guess based on context.
For example:
- ""600 East Broad Street, Richmond, Virginia"" should return coordinates near Richmond
- ""Main Street"" without a city should return null values";

            var model = _config.DefaultModel;
            var isGpt5Model = model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase);
            
            object payload;
            if (isGpt5Model)
            {
                payload = new
                {
                    model = model,
                    response_format = new { type = "json_object" },
                    messages = new[]
                    {
                        new { role = "user", content = geocodePrompt }
                    }
                };
            }
            else
            {
                payload = new
                {
                    model = model,
                    temperature = 0.1,
                    response_format = new { type = "json_object" },
                    messages = new[]
                    {
                        new { role = "user", content = geocodePrompt }
                    }
                };
            }

            var response = await _httpClient.PostAsJsonAsync("chat/completions", payload, SerializerOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Geocoding failed for address: {Address}", address);
                return null;
            }

            var completion = await response.Content.ReadFromJsonAsync<LocationCompletionResponse>(SerializerOptions, cancellationToken);
            var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            _logger.LogInformation("Geocoding response for '{Address}': {Content}", address, content);

            var geocoded = JsonSerializer.Deserialize<GeocodeResult>(content, SerializerOptions);
            return geocoded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error geocoding address: {Address}", address);
            return null;
        }
    }

    private void ConfigureClient()
    {
        _httpClient.BaseAddress = new Uri(_config.Endpoint.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.ApiKey);
    }

    private sealed record LocationCompletionResponse(ChatChoice[] Choices);
    private sealed record ChatChoice(ChatMessage Message);
    private sealed record ChatMessage(string Role, string Content);

    private sealed record LocationExtractionPayload(
        string? Address,
        string? Raw_Location_Text,
        double? Confidence);

    private sealed record GeocodeResult(
        double? Latitude,
        double? Longitude);
}

