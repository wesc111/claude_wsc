namespace Blackjack.Core;

/// <summary>
/// A shoe of one or more 52-card decks, shuffled with Fisher–Yates.
/// The shoe is shuffled on construction; <see cref="NeedsReshuffle"/> becomes true once the penetration mark is reached.
/// </summary>
public sealed class Shoe : ICardSource
{
    private readonly Card[] _cards;
    private readonly decimal _penetration;
    private readonly Random _random;
    private int _next;

    /// <summary>Creates and shuffles a shoe.</summary>
    /// <param name="decks">Number of decks, from <see cref="Rules.MinDecks"/> to <see cref="Rules.MaxDecks"/>.</param>
    /// <param name="penetration">Fraction of cards dealt before a reshuffle is needed; greater than 0 and at most 1.</param>
    /// <param name="seed">Seed for a reproducible card sequence, or <c>null</c> for a random one.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="decks"/> or <paramref name="penetration"/> is out of range.</exception>
    public Shoe(int decks, decimal penetration, int? seed = null)
    {
        Rules.ValidateDecks(decks, nameof(decks));
        Rules.ValidatePenetration(penetration, nameof(penetration));

        _penetration = penetration;
        // Never Random.Shared: parallel simulation workers must stay independent.
        _random = seed is { } s ? new Random(s) : new Random();
        _cards = new Card[decks * 52];

        var i = 0;
        for (var d = 0; d < decks; d++)
        {
            foreach (var suit in Enum.GetValues<Suit>())
            {
                foreach (var rank in Enum.GetValues<Rank>())
                {
                    _cards[i++] = new Card(rank, suit);
                }
            }
        }

        Shuffle();
    }

    /// <summary>Creates and shuffles a shoe with the deck count and penetration of <paramref name="rules"/>.</summary>
    /// <param name="rules">The table rules.</param>
    /// <param name="seed">Seed for a reproducible card sequence, or <c>null</c> for a random one.</param>
    public Shoe(Rules rules, int? seed = null)
        : this(rules?.Decks ?? throw new ArgumentNullException(nameof(rules)), rules.Penetration, seed)
    {
    }

    /// <inheritdoc />
    public int TotalCards => _cards.Length;

    /// <inheritdoc />
    public int CardsRemaining => _cards.Length - _next;

    /// <inheritdoc />
    public bool NeedsReshuffle => _next >= _cards.Length * _penetration;

    /// <inheritdoc />
    public Card Draw()
    {
        if (_next >= _cards.Length)
        {
            throw new InvalidOperationException("The shoe is empty.");
        }

        return _cards[_next++];
    }

    /// <inheritdoc />
    public void Shuffle()
    {
        // All cards stay in the array, so resetting the position returns the dealt ones to the shoe.
        _next = 0;
        for (var i = _cards.Length - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }
    }
}
