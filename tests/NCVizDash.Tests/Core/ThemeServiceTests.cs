using Microsoft.Extensions.Logging.Abstractions;
using NCVizDash.TaskPane.Services;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>Unit tests for <see cref="ThemeService"/>.</summary>
public sealed class ThemeServiceTests
{
    [Fact]
    public void ApplyTheme_RaisesThemeChangedWithCorrectValue()
    {
        var sut = new ThemeService(NullLogger<ThemeService>.Instance);
        string? receivedTheme = null;

        sut.ThemeChanged += (_, theme) => receivedTheme = theme;
        sut.ApplyTheme("Dark");

        Assert.Equal("Dark", receivedTheme);
    }

    [Fact]
    public void ApplyTheme_NoSubscribers_DoesNotThrow()
    {
        var sut = new ThemeService(NullLogger<ThemeService>.Instance);

        var exception = Record.Exception(() => sut.ApplyTheme("Light"));

        Assert.Null(exception);
    }

    [Fact]
    public void ApplyTheme_MultipleSubscribers_AllNotified()
    {
        var sut = new ThemeService(NullLogger<ThemeService>.Instance);
        var callCount = 0;

        sut.ThemeChanged += (_, _) => callCount++;
        sut.ThemeChanged += (_, _) => callCount++;
        sut.ApplyTheme("Dark");

        Assert.Equal(2, callCount);
    }
}
