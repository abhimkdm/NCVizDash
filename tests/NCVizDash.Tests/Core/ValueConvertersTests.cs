using System.Windows;
using NCVizDash.TaskPane.Converters;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>Unit tests for the WPF value converters used across task pane views.</summary>
public sealed class ValueConvertersTests
{
    [Fact]
    public void BooleanToVisibilityConverter_True_ReturnsVisible()
    {
        var sut = new BooleanToVisibilityConverter();
        var result = sut.Convert(true, typeof(Visibility), null, null!);
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void BooleanToVisibilityConverter_False_ReturnsCollapsed()
    {
        var sut = new BooleanToVisibilityConverter();
        var result = sut.Convert(false, typeof(Visibility), null, null!);
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void GridUnitConverter_Convert_MultipliesByUnitSize()
    {
        var sut = new GridUnitConverter();
        var result = sut.Convert(3, typeof(double), null, null!);
        Assert.Equal(120d, result);
    }

    [Fact]
    public void GridUnitConverter_ConvertBack_DividesAndRounds()
    {
        var sut = new GridUnitConverter();
        var result = sut.ConvertBack(125d, typeof(int), null, null!);
        Assert.Equal(3, result);
    }

    [Fact]
    public void InverseBooleanConverter_True_ReturnsFalse()
    {
        var sut = new InverseBooleanConverter();
        var result = sut.Convert(true, typeof(bool), null, null!);
        Assert.Equal(false, result);
    }

    [Fact]
    public void CountToVisibilityConverter_PositiveCount_ReturnsVisible()
    {
        var sut = new CountToVisibilityConverter();
        var result = sut.Convert(5, typeof(Visibility), null, null!);
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void CountToVisibilityConverter_ZeroCount_ReturnsCollapsed()
    {
        var sut = new CountToVisibilityConverter();
        var result = sut.Convert(0, typeof(Visibility), null, null!);
        Assert.Equal(Visibility.Collapsed, result);
    }
}
