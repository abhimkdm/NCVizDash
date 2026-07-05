using Microsoft.Extensions.DependencyInjection;

namespace NCVizDash.Core.DependencyInjection;

/// <summary>
/// Extension methods for registering NCVizDash.Core services into
/// the shared <see cref="IServiceCollection"/>.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers all cross-cutting Core services.
    /// Call this from the add-in startup before project-specific registrations.
    /// </summary>
    public static IServiceCollection AddNCVizDashCore(this IServiceCollection services)
    {
        // Core services that have no VSTO dependency are registered here.
        // Concrete implementations provided by Infrastructure and add-in projects
        // are registered via their own extension methods and must be added after this call.
        return services;
    }
}
