using System.Globalization;

namespace Blackjack.Core.Tests.Helpers;

/// <summary>Parses short card lists such as "A K 10 6" or "AH 10S" (suit letter optional, default spades).</summary>
public static class Cards
{
    public static IReadOnlyList<Card> Parse(string cards) =>
        cards.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(ParseOne).ToArray();

    public static Card ParseOne(string token)
    {
        var suit = Suit.Spades;
        var last = char.ToUpperInvariant(token[^1]);
        if (token.Length > 1 && "CDHS".Contains(last, StringComparison.Ordinal))
        {
            suit = last switch { 'C' => Suit.Clubs, 'D' => Suit.Diamonds, 'H' => Suit.Hearts, _ => Suit.Spades };
            token = token[..^1];
        }

        var rank = token.ToUpperInvariant() switch
        {
            "A" => Rank.Ace,
            "K" => Rank.King,
            "Q" => Rank.Queen,
            "J" => Rank.Jack,
            var n => (Rank)int.Parse(n, CultureInfo.InvariantCulture),
        };
        return new Card(rank, suit);
    }
}
