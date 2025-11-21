using Microsoft.Extensions.Options;
using Prepared.Business.Interfaces;
using Prepared.Business.Options;

namespace Prepared.Business.Services;

/// <summary>
/// Configuration service for Twilio Media Stream settings.
/// Reads configuration values from validated options with sensible defaults.
/// </summary>
public class MediaStreamConfigurationService(IOptions<MediaStreamOptions> options) : IMediaStreamConfigurationService
{
    private readonly MediaStreamOptions _options = options.Value;

    public double AudioBufferSeconds => _options.AudioBufferSeconds;

    public double SilenceThreshold => _options.SilenceThreshold;

    public int SampleRate => _options.SampleRate;
}

