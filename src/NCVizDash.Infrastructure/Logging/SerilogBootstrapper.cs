using System.IO;
using Serilog;
using Serilog.Events;
using NCVizDash.Models;

namespace NCVizDash.Infrastructure.Logging;

/// <summary>
/// Bootstraps the Serilog pipeline from <see cref="AppSettings"/>.
/// Call <see cref="CreateLogger"/> once at add-in startup.
/// </summary>
public static class SerilogBootstrapper
{
    private static ILogger? _rootLogger;

    /// <summary>
    /// Creates and assigns the Serilog root logger.
    /// Safe to call multiple times – subsequent calls reconfigure the logger.
    /// </summary>
    /// <param name="settings">Application settings that drive log level and path.</param>
    /// <returns>The configured <see cref="ILogger"/> instance.</returns>
    public static ILogger CreateLogger(AppSettings settings)
    {
        var level = settings.LogLevel switch
        {
            "Verbose"     => LogEventLevel.Verbose,
            "Debug"       => LogEventLevel.Debug,
            "Warning"     => LogEventLevel.Warning,
            "Error"       => LogEventLevel.Error,
            "Fatal"       => LogEventLevel.Fatal,
            _             => LogEventLevel.Information
        };

        var logDir = Environment.ExpandEnvironmentVariables(settings.LogDirectory);
        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, "ncvizdash-.log");

        _rootLogger = new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("Application", "NCVizDash")
            .WriteTo.Debug(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] " +
                    "({ThreadId}) {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Logger = _rootLogger;
        return _rootLogger;
    }

    /// <summary>Flushes and closes the Serilog pipeline. Call on add-in shutdown.</summary>
    public static void CloseAndFlush() => Log.CloseAndFlush();
}
