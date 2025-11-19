using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Prepared.Data.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register data services here
        // Example:
        // services.AddScoped<ISomeRepository, SomeRepository>();
        // services.AddDbContext<ApplicationDbContext>(options =>
        //     options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        return services;
    }
}

