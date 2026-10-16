using System.Globalization;

namespace Blackjack.ConsoleApp;

/// <summary>Formats and parses money amounts for the console.</summary>
internal static class Money
{
    /// <summary>Formats without unnecessary decimals and with '.' as decimal point, e.g. <c>22.5</c>, <c>975</c>.</summary>
    public static string Format(decimal amount) => amount.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Formats a net amount with sign, e.g. <c>+15</c>, <c>-25</c>, <c>0</c>.</summary>
    public static string FormatSigned(decimal amount) => amount > 0 ? "+" + Format(amount) : Format(amount);

    /// <summary>Parses an amount; both '.' and ',' are accepted as decimal separator.</summary>
    public static bool TryParse(string text, out decimal amount) =>
        decimal.TryParse(
            text.Trim().Replace(',', '.'),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out amount);
}
