namespace Blackjack.ConsoleApp;

/// <summary>Formats cards as text such as <c>A♠ 10♥</c>; red suits optionally in red, hidden cards as <c>[??]</c>.</summary>
internal sealed class CardFormatter(bool useColor)
{
    private const string Red = "\u001b[31m";
    private const string Reset = "\u001b[0m";

    /// <summary>The text shown for a face-down card.</summary>
    public const string Hidden = "[??]";

    /// <summary>True if the console output can show colors: not redirected and <c>NO_COLOR</c> not set.</summary>
    public static bool ConsoleSupportsColor() =>
        !Console.IsOutputRedirected && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));

    /// <summary>Formats one card, or <see cref="Hidden"/> for <c>null</c>.</summary>
    public string Format(Card? card)
    {
        if (card is not { } c)
        {
            return Hidden;
        }

        var red = c.Suit is Suit.Hearts or Suit.Diamonds;
        return useColor && red ? Red + c + Reset : c.ToString();
    }

    /// <summary>Formats several cards separated by spaces.</summary>
    public string Format(IEnumerable<Card?> cards) => string.Join(" ", cards.Select(Format));
}
