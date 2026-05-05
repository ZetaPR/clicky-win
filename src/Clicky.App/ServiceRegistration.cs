using Microsoft.Extensions.DependencyInjection;

namespace Clicky.App;

public static class ServiceRegistration
{
    /// <summary>Registers all Clicky application services.</summary>
    public static IServiceCollection AddClickyServices(this IServiceCollection services)
    {
        // Services registered here as tasks add them
        return services;
    }
}
