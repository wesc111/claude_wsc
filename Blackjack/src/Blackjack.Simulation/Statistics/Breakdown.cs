namespace Blackjack.Simulation.Statistics;

/// <summary>The breakdown keys: dealer upcard and player starting total.</summary>
internal static class Breakdown
{
    /// <summary>Dealer upcard keys in display order; all ten-valued cards share the key "10".</summary>
    public static IReadOnlyList<string> UpcardKeys { get; } = ["2", "3", "4", "5", "6", "7", "8", "9", "10", "A"];

    /// <summary>Starting total keys in display order: hard 4–20, soft 12–20, Blackjack.</summary>
    public static IReadOnlyList<string> StartingTotalKeys { get; } =
    [
        .. Enumerable.Range(4, 17).Select(t => $"hard {t}"),
        .. Enumerable.Range(12, 9).Select(t => $"soft {t}"),
        "Blackjack",
    ];

    /// <summary>Index into <see cref="UpcardKeys"/>.</summary>
    public static int UpcardIndex(Card upcard) => upcard.IsAce ? 9 : upcard.BaseValue - 2;

    /// <summary>Index into <see cref="StartingTotalKeys"/> for the first two player cards.</summary>
    public static int StartingTotalIndex(Card first, Card second)
    {
        var total = first.BaseValue + second.BaseValue;
        if (total == 22)
        {
            total = 12; // A, A: one Ace counts 1.
        }

        if (first.IsAce || second.IsAce)
        {
            return total == 21 ? 26 : 17 + (total - 12);
        }

        return total - 4;
    }
}
