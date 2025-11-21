using Microsoft.Extensions.Options;
using Prepared.Business.Interfaces;
using Prepared.Business.Options;

namespace Prepared.Business.Services;

/// <summary>
/// Configuration service for Whisper transcription settings.
/// Reads configuration values from validated options with sensible defaults.
/// </summary>
public class WhisperConfigurationService(IOptions<WhisperOptions> options) : IWhisperConfigurationService
{
    private readonly WhisperOptions _options = options.Value;

    public string ApiKey => _options.ApiKey;

    public string Model => _options.Model;

    public string Endpoint => _options.Endpoint;

    public double Temperature => _options.Temperature;

    public int TimeoutSeconds => _options.TimeoutSeconds;
}

