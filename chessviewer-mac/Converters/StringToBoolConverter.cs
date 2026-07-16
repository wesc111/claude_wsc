using System.Globalization;
using Avalonia.Data.Converters;

namespace ChessViewer.Converters;

// Avalonia's Border/TextBlock use a bool IsVisible property rather than WPF's Visibility enum.
public class StringToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && s.Length > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
