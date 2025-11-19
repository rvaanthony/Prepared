using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prepared.Business.Interfaces;
using Prepared.Business.Options;
using Prepared.Business.Services;

namespace Prepared.Business.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBusinessServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Note: ITranscriptHub must be registered in the Client project before calling this
        // because it depends on SignalR infrastructure (IHubContext)
        
        services.Configure<WhisperOptions>(configuration.GetSection(WhisperOptions.SectionName));

        // Register HTTP clients/services
        services.AddHttpClient<ITranscriptionService, WhisperTranscriptionService>();

        // Register Twilio services
        services.AddScoped<ITwilioService, TwilioService>();
        services.AddScoped<IMediaStreamService, MediaStreamService>();

        // Additional business services will be registered here as they are implemented
        // Example:
        // services.AddScoped<ITranscriptionService, TranscriptionService>();
        // services.AddScoped<ILocationExtractionService, LocationExtractionService>();

        return services;
    }
}

