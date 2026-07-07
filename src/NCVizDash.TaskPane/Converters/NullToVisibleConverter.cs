using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NCVizDash.TaskPane.Converters;

/// <summary>
/// Opposite of NullToCollapsedConverter: returns Visible when the bound value
/// is null, Collapsed when it's non-null. Used for "empty state" placeholders
/// that should show only in the absence of real content — deliberately a
/// separate converter with no parameter, rather than reusing
/// NullToCollapsedConverter with an "Invert" ConverterParameter, since that
/// parameter was never actually implemented and caused this exact overlap bug.
/// </summary>
public sealed class NullToVisibleConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
