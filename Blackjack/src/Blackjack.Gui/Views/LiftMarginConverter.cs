using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Blackjack.Gui.Views;

/// <summary>Converts a lift in pixels into a bottom margin, so chips of a stack sit on top of each other.</summary>
public sealed class LiftMarginConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double lift ? new Thickness(0, 0, 0, lift) : new Thickness(0);

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
