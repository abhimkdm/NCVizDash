using NCVizDash.Models;
using Xunit;

namespace NCVizDash.Tests.Infrastructure;

/// <summary>Unit tests for <see cref="AppSettings"/> defaults and validation.</summary>
public sealed class AppSettingsTests
{
    [Fact]
    public void AppSettings_DefaultTheme_IsLight()
    {
        var settings = new AppSettings();
        Assert.Equal("Light", settings.DefaultTheme);
    }

    [Fact]
    public void AppSettings_DefaultLogLevel_IsInformation()
    {
        var settings = new AppSettings();
        Assert.Equal("Information", settings.LogLevel);
    }

    [Fact]
    public void AppSettings_MaxIngestRows_IsOneMillion()
    {
        var settings = new AppSettings();
        Assert.Equal(1_000_000L, settings.MaxIngestRows);
    }

    [Fact]
    public void AppSettings_TelemetryEnabled_DefaultFalse()
    {
        var settings = new AppSettings();
        Assert.False(settings.TelemetryEnabled);
    }

    [Fact]
    public void AppSettings_AutoRefreshSeconds_DefaultZero()
    {
        var settings = new AppSettings();
        Assert.Equal(0, settings.AutoRefreshSeconds);
    }
}
