using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        // Register unified insights service for efficient single-call extraction
        // Use longer timeout for gpt-5-mini which can take 30-60 seconds to respond
        services.AddHttpClient<IUnifiedInsightsService, UnifiedInsightsService>((sp, client) =>
        {
            var config = sp.GetRequiredService<IOpenAiConfigurationService>();
            // Set HTTP client timeout to 90 seconds for gpt-5-mini (default resilience handler timeout is 10s, but HTTP timeout will be the limiting factor)
            // The default resilience handler's attempt timeout (10s) will be overridden by the longer HTTP client timeout
            client.Timeout = TimeSpan.FromSeconds(Math.Max(config.TimeoutSeconds, 90));
        });

        // Register Twilio services
        services.AddScoped<ITwilioService, TwilioService>();
        services.AddScoped<IMediaStreamService, MediaStreamService>();

        return services;
    }
}

