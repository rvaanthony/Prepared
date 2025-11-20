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
        services.AddHttpClient<ITranscriptionService, WhisperTranscriptionService>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<WhisperOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        services.AddHttpClient<ISummarizationService, OpenAiSummarizationService>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        services.AddHttpClient<ILocationExtractionService, OpenAiLocationExtractionService>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        // Register unified insights service for efficient single-call extraction
        services.AddHttpClient<UnifiedInsightsService>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        // Register Twilio services
        services.AddScoped<ITwilioService, TwilioService>();
        services.AddScoped<IMediaStreamService, MediaStreamService>();

        return services;
    }
}

