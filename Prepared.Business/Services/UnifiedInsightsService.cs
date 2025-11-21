using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Prepared.Business.Interfaces;
using Prepared.Common.Models;

namespace Prepared.Business.Services;

/// <summary>
/// Extracts location, summary, and key findings from a transcript in one OpenAI request.
/// </summary>
public class UnifiedInsightsService : IUnifiedInsightsService
{
    private readonly HttpClient _httpClient;
    private readonly IOpenAiConfigurationService _config;
    private readonly ILogger<UnifiedInsightsService> _logger;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public UnifiedInsightsService(
        HttpClient httpClient,
        IOpenAiConfigurationService config,
        ILogger<UnifiedInsightsService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ConfigureClient();
    }

    /// <summary>
    /// Extracts unified insights (location, summary, key findings) from a call transcript in a single API call.
    /// </summary>
    /// <param name="callSid">The unique identifier for the call.</param>
    /// <param name="transcript">The transcript text to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Unified insights result containing location, summary, and key findings, or null if extraction fails or transcript is empty.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="callSid"/> is null or empty.</exception>
    public async Task<UnifiedInsightsResult?> ExtractInsightsAsync(
        string callSid, 
        string transcript, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callSid))
            throw new ArgumentException("CallSid cannot be null or empty", nameof(callSid));
            
        if (string.IsNullOrWhiteSpace(transcript))
        {
            _logger.LogDebug("Skipping insights extraction - transcript is empty: CallSid={CallSid}", callSid);
            return null;
        }

        try
        {
            var systemPrompt = @"You are an AI assistant for emergency dispatch analyzing 911 call transcripts.

TASK: Extract ALL relevant information from the transcript in ONE structured response:
1. LOCATION (if mentioned) - Extract address and provide approximate coordinates
2. SUMMARY - Brief overview of the incident
3. KEY FINDINGS - Important details (injuries, hazards, urgency, etc.)

LOCATION EXTRACTION:
- Look for: street addresses, intersections, landmarks, business names, apartment numbers
- Extract COMPLETE address with all details (street number, name, city, state, zip)
- Provide BEST GUESS coordinates (latitude, longitude) based on your knowledge
- Include confidence score (0.0 to 1.0)
- If NO location mentioned, return null for location fields

SUMMARY:
- One sentence overview of the incident
- Include: what happened, where (if known), severity

KEY FINDINGS:
- Extract 2-5 critical points
- Include: injuries, hazards, number of people involved, urgency indicators
- Prioritize actionable information for first responders

EXAMPLES:

Input: ""Hey, I'm at 600 East Broad Street in Richmond, Virginia. There's a fire in the building. Two people are trapped on the second floor.""
Output:
{
  ""location"": {
    ""address"": ""600 East Broad Street, Richmond, Virginia"",
    ""latitude"": 37.5407,
    ""longitude"": -77.4360,
    ""confidence"": 0.9
  },
  ""summary"": ""Structure fire at 600 East Broad Street, Richmond, Virginia with two people trapped on second floor"",
  ""key_findings"": [
    ""Active fire in building"",
    ""Two people trapped on second floor"",
    ""Immediate rescue required""
  ]
}

Input: ""I just witnessed a car accident on Highway 101 near the Woodside Road exit. Multiple vehicles involved, looks serious.""
Output:
{
  ""location"": {
    ""address"": ""Highway 101 near Woodside Road exit"",
    ""latitude"": 37.4275,
    ""longitude"": -122.2305,
    ""confidence"": 0.7
  },
  ""summary"": ""Multi-vehicle accident on Highway 101 near Woodside Road exit"",
  ""key_findings"": [
    ""Multiple vehicles involved"",
    ""Appears to be serious injuries"",
    ""Major highway - traffic hazard""
  ]
}

Input: ""Someone stole my wallet at the coffee shop. I just noticed it's missing.""
Output:
{
  ""location"": null,
  ""summary"": ""Wallet theft reported at coffee shop"",
  ""key_findings"": [
    ""Non-emergency theft"",
    ""No immediate danger"",
    ""Location not specified""
  ]
}

Return ONLY valid JSON with this EXACT structure:
{
  ""location"": {
    ""address"": ""full address or null"",
    ""latitude"": 37.1234 or null,
    ""longitude"": -122.5678 or null,
    ""confidence"": 0.9
  },
  ""summary"": ""one sentence summary"",
  ""key_findings"": [""finding 1"", ""finding 2"", ""finding 3""]
}";

            // Check if using gpt-5 models (gpt-5-mini, gpt-5, etc.) - they don't support temperature
            var modelName = _config.DefaultModel;
            var isGpt5Model = modelName.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase);
            
            object payload;
            if (isGpt5Model)
            {
                payload = new
                {
                    model = modelName,
                    response_format = new { type = "json_object" },
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = $"Extract insights from this 911 call transcript:\n\n{transcript}" }
                    }
                };
            }
            else
            {
                payload = new
                {
                    model = modelName,
                    temperature = 0.1, // Low temperature for consistent extraction (not supported by gpt-5 models)
                    response_format = new { type = "json_object" },
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = $"Extract insights from this 911 call transcript:\n\n{transcript}" }
                    }
                };
            }

            var response = await _httpClient.PostAsJsonAsync("chat/completions", payload, SerializerOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Unified insights extraction failed ({StatusCode}): {Message}",
                    response.StatusCode, message);
                return null;
            }

            var completion = await response.Content.ReadFromJsonAsync<CompletionResponse>(SerializerOptions, cancellationToken);
            var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogDebug("OpenAI returned empty content for insights: CallSid={CallSid}", callSid);
                return null;
            }

            _logger.LogInformation(
                "Unified insights extraction response: CallSid={CallSid}, Content={Content}",
                callSid, content);

            var parsed = JsonSerializer.Deserialize<UnifiedInsightsPayload>(content, SerializerOptions);
            if (parsed == null)
            {
                _logger.LogWarning("Failed to parse unified insights response: CallSid={CallSid}", callSid);
                return null;
            }

            // Build the result
            var result = new UnifiedInsightsResult
            {
                CallSid = callSid,
                Summary = !string.IsNullOrWhiteSpace(parsed.Summary) 
                    ? new TranscriptSummary
                    {
                        CallSid = callSid,
                        Summary = parsed.Summary,
                        KeyFindings = parsed.Key_Findings ?? Array.Empty<string>()
                    }
                    : null
            };

            // Add location if found
            if (parsed.Location != null && 
                !string.IsNullOrWhiteSpace(parsed.Location.Address) &&
                parsed.Location.Latitude.HasValue && 
                parsed.Location.Longitude.HasValue)
            {
                result.Location = new LocationExtractionResult
                {
                    CallSid = callSid,
                    FormattedAddress = parsed.Location.Address,
                    Latitude = parsed.Location.Latitude.Value,
                    Longitude = parsed.Location.Longitude.Value,
                    Confidence = parsed.Location.Confidence ?? 0.0,
                    RawLocationText = parsed.Location.Address
                };

                _logger.LogInformation(
                    "Unified extraction found location: CallSid={CallSid}, Address={Address}, Lat={Lat}, Lng={Lng}",
                    callSid, result.Location.FormattedAddress, result.Location.Latitude, result.Location.Longitude);
            }

            _logger.LogInformation(
                "Unified insights extracted: CallSid={CallSid}, HasLocation={HasLoc}, HasSummary={HasSum}, FindingsCount={Count}",
                callSid, result.Location != null, result.Summary != null, result.Summary?.KeyFindings?.Count ?? 0);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unified insights extraction error: CallSid={CallSid}", callSid);
            return null;
        }
    }

    private void ConfigureClient()
    {
        _httpClient.BaseAddress = new Uri(_config.Endpoint.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.ApiKey);
    }

    private sealed record CompletionResponse(ChatChoice[] Choices);
    private sealed record ChatChoice(ChatMessage Message);
    private sealed record ChatMessage(string Role, string Content);

    private sealed record UnifiedInsightsPayload(
        LocationPayload? Location,
        string? Summary,
        string[]? Key_Findings);

    private sealed record LocationPayload(
        string? Address,
        double? Latitude,
        double? Longitude,
        double? Confidence);
}

/// <summary>
/// Result containing all insights extracted in a single API call
/// </summary>
public class UnifiedInsightsResult
{
    public required string CallSid { get; init; }
    public LocationExtractionResult? Location { get; set; }
    public TranscriptSummary? Summary { get; set; }
}

