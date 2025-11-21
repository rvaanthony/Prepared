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
/// OpenAI-powered summarization service that generates concise summaries from call transcripts.
/// Uses GPT models to extract key points and create structured summaries.
/// </summary>
public class OpenAiSummarizationService : ISummarizationService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiSummarizationService> _logger;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public OpenAiSummarizationService(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiSummarizationService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ConfigureClient();
    }

    /// <summary>
    /// Generates a summary and key findings from a call transcript using OpenAI.
    /// </summary>
    /// <param name="callSid">The unique identifier for the call.</param>
    /// <param name="transcript">The transcript text to summarize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A transcript summary with key findings, or null if summarization fails or transcript is empty.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="callSid"/> is null or empty.</exception>
    public async Task<TranscriptSummary?> SummarizeAsync(string callSid, string transcript, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callSid))
            throw new ArgumentException("CallSid cannot be null or empty", nameof(callSid));
            
        if (string.IsNullOrWhiteSpace(transcript))
        {
            _logger.LogDebug("Skipping summarization (empty transcript): CallSid={CallSid}", callSid);
            return null;
        }

        try
        {
            var model = _options.SummarizationModel ?? _options.DefaultModel;
            var isGpt5Model = model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase);
            
            object payload;
            if (isGpt5Model)
            {
                payload = new
                {
                    model,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a senior incident dispatcher assistant. Summarize emergency call transcripts with clear bullet points." },
                        new { role = "user", content = transcript }
                    }
                };
            }
            else
            {
                payload = new
                {
                    model,
                    temperature = 0.2,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a senior incident dispatcher assistant. Summarize emergency call transcripts with clear bullet points." },
                        new { role = "user", content = transcript }
                    }
                };
            }

            var response = await _httpClient.PostAsJsonAsync("chat/completions", payload, SerializerOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("OpenAI summary failed ({StatusCode}): {Message}", response.StatusCode, message);
                return null;
            }

            var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(SerializerOptions, cancellationToken);
            var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            var summary = new TranscriptSummary
            {
                CallSid = callSid,
                Summary = content.Trim(),
                KeyFindings = ExtractKeyPoints(content),
                GeneratedAtUtc = DateTime.UtcNow
            };

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI summary error: CallSid={CallSid}", callSid);
            return null;
        }
    }

    private void ConfigureClient()
    {
        _httpClient.BaseAddress = new Uri(_options.Endpoint.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    private static IReadOnlyList<string> ExtractKeyPoints(string content)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Where(l => l.StartsWith("-") || l.StartsWith("*")).ToArray();
    }

    private sealed record ChatCompletionResponse(ChatChoice[] Choices);
    private sealed record ChatChoice(ChatMessage Message);
    private sealed record ChatMessage(string Role, string Content);
}

