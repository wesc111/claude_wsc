using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Blackjack.Gui.Views;

/// <summary>Converts a hex colour (e.g. <c>#c62828</c>) into a solid brush.</summary>
public sealed class ColorBrushConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string color ? new SolidColorBrush(Color.Parse(color)) : null;

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
