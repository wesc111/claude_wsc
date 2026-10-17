using Blackjack.Core;

namespace Blackjack.TestSupport;

/// <summary>
/// Deals a predefined card sequence. Drawing past the end throws, so tests detect unexpected draws.
/// Deal order of a round: player, dealer upcard, player, dealer hole card, then player hits, then dealer draws.
/// </summary>
public sealed class FixedCardSource : ICardSource
{
    private readonly IReadOnlyList<Card> _cards;
    private int _next;

    public FixedCardSource(IEnumerable<Card> cards)
    {
        _cards = cards.ToArray();
    }

    public FixedCardSource(string cards)
        : this(Cards.Parse(cards))
    {
    }

    public bool NeedsReshuffle { get; set; }

    public int ShuffleCount { get; private set; }

    public int CardsDrawn => _next;

    public int CardsRemaining => _cards.Count - _next;

    public int TotalCards => _cards.Count;

    public Card Draw()
    {
        if (_next >= _cards.Count)
        {
            throw new InvalidOperationException($"Unexpected draw: all {_cards.Count} predefined cards have been dealt.");
        }

        return _cards[_next++];
    }

    public void Shuffle() => ShuffleCount++;
}
