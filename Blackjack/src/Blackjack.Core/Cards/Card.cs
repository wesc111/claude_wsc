namespace Blackjack.Core;

/// <summary>A playing card.</summary>
/// <param name="Rank">The rank of the card.</param>
/// <param name="Suit">The suit of the card.</param>
public readonly record struct Card(Rank Rank, Suit Suit)
{
    /// <summary>True for Ten, Jack, Queen and King.</summary>
    public bool IsTenValued => Rank is Rank.Ten or Rank.Jack or Rank.Queen or Rank.King;

    /// <summary>True for an Ace.</summary>
    public bool IsAce => Rank == Rank.Ace;

    /// <summary>The Blackjack value of the card, with an Ace counted as 11.</summary>
    public int BaseValue => Rank switch
    {
        Rank.Ace => 11,
        Rank.Jack or Rank.Queen or Rank.King => 10,
        _ => (int)Rank,
    };

    /// <summary>Returns a short form such as <c>A♠</c> or <c>10♥</c>.</summary>
    public override string ToString()
    {
        var rank = Rank switch
        {
            Rank.Ace => "A",
            Rank.King => "K",
            Rank.Queen => "Q",
            Rank.Jack => "J",
            _ => ((int)Rank).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        var suit = Suit switch
        {
            Suit.Clubs => "♣",
            Suit.Diamonds => "♦",
            Suit.Hearts => "♥",
            _ => "♠",
        };
        return rank + suit;
    }
}
