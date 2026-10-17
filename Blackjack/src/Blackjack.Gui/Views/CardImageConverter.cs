using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Blackjack.Gui.Views;

/// <summary>Converts a card image name (e.g. <c>ace_of_spades</c>) into the embedded PNG bitmap.</summary>
public sealed class CardImageConverter : IValueConverter
{
    private readonly Dictionary<string, Bitmap> _cache = [];

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string name)
        {
            return null;
        }

        if (!_cache.TryGetValue(name, out var bitmap))
        {
            using var stream = AssetLoader.Open(new Uri($"avares://BlackjackGui/Assets/Cards/{name}.png"));
            bitmap = new Bitmap(stream);
            _cache[name] = bitmap;
        }

        return bitmap;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
