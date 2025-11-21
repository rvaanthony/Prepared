using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prepared.Data.Interfaces;
using Prepared.Data.Repositories;
using Prepared.Data.Services;

namespace Prepared.Data.Extensions;

/// <summary>
/// Extension methods for registering data layer services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all data access services including repositories and Azure Table Storage services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.</exception>
    public static IServiceCollection AddDataServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // The configuration service is a read-only container for configuration values.
        services.AddSingleton<IDataConfigurationService, DataConfigurationService>();

        // Register Azure Table Storage services.
        services.AddAzureTableService();

        // Register repositories (scoped for per-request lifetime)
        services.AddScoped<ICallRepository, CallRepository>();
        services.AddScoped<ITranscriptRepository, TranscriptRepository>();
        services.AddScoped<ISummaryRepository, SummaryRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();

        return services;
    }

    /// <summary>
    /// Registers the Azure Table Storage related services as singletons.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    private static void AddAzureTableService(this IServiceCollection services)
    {
        // Register the Table Storage service factory.
        services.AddSingleton<ITableStorageServiceFactory, TableStorageServiceFactory>();

        // Register the Table Storage service using the provided connection string.
        services.AddSingleton<ITableStorageService>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var factory = sp.GetRequiredService<ITableStorageServiceFactory>();
            var connectionString = configuration["AzureStorage:ConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("AzureStorage:ConnectionString is missing in configuration.");

            return factory.Create(connectionString);
        });
    }
}

