using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NCVizDash.TaskPane.Converters;

/// <summary>Converts a <see cref="bool"/> to <see cref="Visibility"/> (true → Visible, false → Collapsed).</summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

/// <summary>
/// Converts a dashboard grid unit (column/row index or span) into device pixels
/// for canvas positioning. One grid unit = 40px, matching the canvas grid overlay tile size.
/// </summary>
public sealed class GridUnitConverter : IValueConverter
{
    /// <summary>Pixel size of one grid unit. Matches the visual grid overlay in CanvasPanelView.</summary>
    public const double UnitSize = 40d;

    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i ? i * UnitSize : 0d;

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double d ? (int)Math.Round(d / UnitSize) : 0;
}

/// <summary>Inverts a boolean value. Useful for IsEnabled bindings tied to a loading flag.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !(value is true);

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !(value is true);
}

/// <summary>Joins a filter's value list into a comma-separated display string (used in filter-chip tooltips).</summary>
public sealed class FilterValuesToStringConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is IEnumerable<string> values ? string.Join(", ", values) : string.Empty;

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Returns Visibility.Collapsed when the bound value is null, Visible otherwise.</summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Returns Visibility.Visible when a bound count is greater than zero, Collapsed otherwise.</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
