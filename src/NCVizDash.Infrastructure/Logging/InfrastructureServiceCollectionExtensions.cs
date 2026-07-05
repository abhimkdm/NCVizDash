using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.Infrastructure.Configuration;
using Serilog;
using Serilog.Extensions.Logging;

namespace NCVizDash.Infrastructure.Logging;

/// <summary>
/// Registers infrastructure services (logging, configuration) into DI.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adds Serilog-backed <see cref="ILoggerFactory"/>,
    /// <see cref="IAppSettingsProvider"/>, and related infrastructure services.
    /// </summary>
    public static IServiceCollection AddNCVizDashInfrastructure(this IServiceCollection services)
    {
        // Configuration / settings
        services.AddSingleton<IAppSettingsProvider, JsonAppSettingsProvider>();

        // Logging – Serilog is bootstrapped in the add-in host using
        // SerilogBootstrapper.CreateLogger(settings) BEFORE DI is built.
        // Here we wire the already-configured Serilog pipeline into
        // Microsoft.Extensions.Logging so every ILogger<T> resolves correctly.
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: false);
        });

        return services;
    }
}
