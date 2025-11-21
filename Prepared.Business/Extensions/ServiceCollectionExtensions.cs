using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Prepared.Business.Interfaces;
using Prepared.Business.Options;
using Prepared.Business.Services;

namespace Prepared.Business.Extensions;

/// <summary>
/// Extension methods for registering business layer services with dependency injection.
/// Includes configuration validation and HTTP client configuration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all business services with proper configuration validation and HTTP client setup.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="environment">Host environment.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBusinessServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Note: ITranscriptHub must be registered in the Client project before calling this
        // because it depends on SignalR infrastructure (IHubContext)
        
        // Configure and validate options
        services.Configure<WhisperOptions>(configuration.GetSection(WhisperOptions.SectionName));
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));
        services.Configure<MediaStreamOptions>(configuration.GetSection(MediaStreamOptions.SectionName));
        
        // Register configuration services (read-only containers for configuration values)
        // These provide consistent access patterns and make testing easier
        services.AddSingleton<ITwilioConfigurationService, TwilioConfigurationService>();
        services.AddSingleton<IOpenAiConfigurationService, OpenAiConfigurationService>();
        services.AddSingleton<IWhisperConfigurationService, WhisperConfigurationService>();
        services.AddSingleton<IMediaStreamConfigurationService, MediaStreamConfigurationService>();

        // Validate options at startup (fail fast if misconfigured)
        services.AddOptions<WhisperOptions>()
            .Bind(configuration.GetSection(WhisperOptions.SectionName))
            .Validate(options =>
            {
                var context = new System.ComponentModel.DataAnnotations.ValidationContext(options);
                var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
                return System.ComponentModel.DataAnnotations.Validator.TryValidateObject(options, context, results, true);
            }, "Whisper configuration validation failed")
            .ValidateOnStart();

        services.AddOptions<OpenAiOptions>()
            .Bind(configuration.GetSection(OpenAiOptions.SectionName))
            .Validate(options =>
            {
                var context = new System.ComponentModel.DataAnnotations.ValidationContext(options);
                var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
                return System.ComponentModel.DataAnnotations.Validator.TryValidateObject(options, context, results, true);
            }, "OpenAI configuration validation failed")
            .ValidateOnStart();

        services.AddOptions<MediaStreamOptions>()
            .Bind(configuration.GetSection(MediaStreamOptions.SectionName))
            .Validate(options =>
            {
                var context = new System.ComponentModel.DataAnnotations.ValidationContext(options);
                var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
                return System.ComponentModel.DataAnnotations.Validator.TryValidateObject(options, context, results, true);
            }, "MediaStream configuration validation failed")
            .ValidateOnStart();

        // Register HTTP clients with explicit timeouts and proper configuration
        // Using configuration services for consistent access patterns
        services.AddHttpClient<ITranscriptionService, WhisperTranscriptionService>((sp, client) =>
        {
            var config = sp.GetRequiredService<IWhisperConfigurationService>();
            client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
        });

        services.AddHttpClient<ISummarizationService, OpenAiSummarizationService>((sp, client) =>
        {
            var config = sp.GetRequiredService<IOpenAiConfigurationService>();
            client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
        });

        services.AddHttpClient<ILocationExtractionService, OpenAiLocationExtractionService>((sp, client) =>
        {
            var config = sp.GetRequiredService<IOpenAiConfigurationService>();
            client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
        });

        // Register unified insights service for efficient single-call extraction.
        // IMPORTANT: gpt-5-mini can take 30-60+ seconds to respond. The global standard resilience handler
        // (from ServiceDefaults) uses a 10-second attempt timeout, which is incompatible with this service.
        // We do NOT add any resilience handler here. Instead, we rely on the HTTP client timeout and
        // the service's own error handling to manage long-running requests gracefully.
        services.AddHttpClient<IUnifiedInsightsService, UnifiedInsightsService>((sp, client) =>
        {
            var config = sp.GetRequiredService<IOpenAiConfigurationService>();
            // Minimum 90 seconds to accommodate gpt-5-mini response times
            var timeoutSeconds = Math.Max(config.TimeoutSeconds, 90);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        })
#pragma warning disable EXTEXP0001 // Remove and reconfigure resilience handler
        .RemoveAllResilienceHandlers()
#pragma warning restore EXTEXP0001
        .AddStandardResilienceHandler()
        .Configure((options, sp) =>
        {
            var config = sp.GetRequiredService<IOpenAiConfigurationService>();
            var timeoutSeconds = Math.Max(config.TimeoutSeconds, 90);
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);

            // Override all timeout values to match the long-running nature of this service
            options.AttemptTimeout.Timeout = timeout;
            options.TotalRequestTimeout.Timeout = timeout.Add(TimeSpan.FromSeconds(5));
            options.CircuitBreaker.SamplingDuration = timeout.Add(timeout);
        });

        // Register Twilio services
        services.AddScoped<ITwilioService, TwilioService>();
        services.AddScoped<IMediaStreamService, MediaStreamService>();

        return services;
    }
}

