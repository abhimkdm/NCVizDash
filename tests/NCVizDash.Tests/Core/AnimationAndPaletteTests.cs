using NCVizDash.ChartEngine;
using NCVizDash.Models;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>Unit tests for <see cref="AnimationPresets"/> and <see cref="ChartPalette"/>.</summary>
public sealed class AnimationAndPaletteTests
{
    [Theory]
    [InlineData(VisualType.Bar)]
    [InlineData(VisualType.Line)]
    [InlineData(VisualType.Area)]
    [InlineData(VisualType.Pie)]
    [InlineData(VisualType.Donut)]
    [InlineData(VisualType.Gauge)]
    [InlineData(VisualType.Radar)]
    [InlineData(VisualType.Scatter)]
    [InlineData(VisualType.Bubble)]
    [InlineData(VisualType.Heatmap)]
    [InlineData(VisualType.Treemap)]
    public void AnimationPresets_EveryChartVisualType_HasAnimationEnabled(VisualType type)
    {
        var preset = AnimationPresets.For(type);
        Assert.True(preset.Animation);
        Assert.True(preset.AnimationDuration > 0);
    }

    [Fact]
    public void AnimationPresets_Gauge_UsesElasticEasing()
    {
        var preset = AnimationPresets.For(VisualType.Gauge);
        Assert.Equal("elasticOut", preset.AnimationEasing);
    }

    [Fact]
    public void AnimationPresets_Bar_UsesBounceEasing()
    {
        var preset = AnimationPresets.For(VisualType.Bar);
        Assert.Equal("bounceOut", preset.AnimationEasing);
    }

    [Fact]
    public void AnimationPresets_Pie_HasStaggeredDelay()
    {
        var preset = AnimationPresets.For(VisualType.Pie);
        Assert.True(preset.AnimationDelay > 0);
    }

    [Fact]
    public void AnimationPresets_ToOptionDict_ContainsAllExpectedKeys()
    {
        var preset = AnimationPresets.For(VisualType.Line);
        var dict = AnimationPresets.ToOptionDict(preset);

        Assert.Contains("animation", dict.Keys);
        Assert.Contains("animationDuration", dict.Keys);
        Assert.Contains("animationEasing", dict.Keys);
        Assert.Contains("animationDurationUpdate", dict.Keys);
        Assert.Contains("animationEasingUpdate", dict.Keys);
        Assert.Contains("animationThreshold", dict.Keys);
    }

    [Fact]
    public void ChartPalette_Light_Has10Colors()
    {
        Assert.Equal(10, ChartPalette.Light.Count);
    }

    [Fact]
    public void ChartPalette_Dark_Has10Colors()
    {
        Assert.Equal(10, ChartPalette.Dark.Count);
    }

    [Fact]
    public void ChartPalette_AllColorsAreValidHex()
    {
        foreach (var color in ChartPalette.Light.Concat(ChartPalette.Dark))
        {
            Assert.StartsWith("#", color);
            Assert.Equal(7, color.Length);
        }
    }

    [Theory]
    [InlineData("Dark", true)]
    [InlineData("Light", false)]
    [InlineData("dark", true)]  // case-insensitive
    public void ChartPalette_Palette_SelectsCorrectSetByTheme(string theme, bool expectDarkSet)
    {
        var palette = ChartPalette.Palette(theme);
        var expected = expectDarkSet ? ChartPalette.Dark : ChartPalette.Light;
        Assert.Equal(expected, palette);
    }

    [Fact]
    public void ChartPalette_Primary_DiffersBetweenThemes()
    {
        Assert.NotEqual(ChartPalette.Primary("Light"), ChartPalette.Primary("Dark"));
    }
}
