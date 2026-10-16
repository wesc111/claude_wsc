namespace Blackjack.Core;

/// <summary>The cards of one hand and their Blackjack evaluation.</summary>
public sealed class Hand
{
    private readonly List<Card> _cards = [];

    /// <summary>Creates an empty hand.</summary>
    public Hand()
    {
        Cards = _cards.AsReadOnly();
    }

    /// <summary>Creates a hand containing <paramref name="cards"/>.</summary>
    /// <param name="cards">The initial cards.</param>
    public Hand(IEnumerable<Card> cards)
        : this()
    {
        ArgumentNullException.ThrowIfNull(cards);
        foreach (var card in cards)
        {
            Add(card);
        }
    }

    /// <summary>The cards in the order they were received.</summary>
    public IReadOnlyList<Card> Cards { get; }

    /// <summary>The best total: Aces count 11 unless that would exceed 21.</summary>
    public int Total { get; private set; }

    /// <summary>True if at least one Ace currently counts as 11.</summary>
    public bool IsSoft { get; private set; }

    /// <summary>True for exactly two cards: an Ace and a ten-valued card.</summary>
    public bool IsBlackjack => _cards.Count == 2 && Total == 21;

    /// <summary>True if the total exceeds 21.</summary>
    public bool IsBust => Total > 21;

    /// <summary>Adds a card to the hand.</summary>
    /// <param name="card">The card to add.</param>
    public void Add(Card card)
    {
        _cards.Add(card);
        Evaluate();
    }

    private void Evaluate()
    {
        var total = 0;
        var acesAsEleven = 0;
        foreach (var card in _cards)
        {
            total += card.BaseValue;
            if (card.IsAce)
            {
                acesAsEleven++;
            }
        }

        while (total > 21 && acesAsEleven > 0)
        {
            total -= 10;
            acesAsEleven--;
        }

        Total = total;
        IsSoft = acesAsEleven > 0;
    }
}
