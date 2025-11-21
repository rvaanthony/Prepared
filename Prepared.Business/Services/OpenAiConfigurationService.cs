using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Prepared.Business.Interfaces;
using Prepared.Business.Options;

namespace Prepared.Business.Services;

/// <summary>
/// Configuration service for OpenAI settings.
/// Reads configuration values from validated options with sensible defaults.
/// </summary>
public class OpenAiConfigurationService(IOptions<OpenAiOptions> options) : IOpenAiConfigurationService
{
    private readonly OpenAiOptions _options = options.Value;

    public string ApiKey => _options.ApiKey;

    public string Endpoint => _options.Endpoint;

    public string DefaultModel => _options.DefaultModel;

    public string SummarizationModel => _options.SummarizationModel ?? _options.DefaultModel;

    public string LocationModel => _options.LocationModel ?? _options.DefaultModel;

    public int TimeoutSeconds => _options.TimeoutSeconds;
}

