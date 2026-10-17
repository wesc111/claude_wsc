namespace Blackjack.Core;

/// <summary>A source of cards for the engine, such as a <see cref="Shoe"/>.</summary>
public interface ICardSource
{
    /// <summary>Draws the next card.</summary>
    /// <exception cref="InvalidOperationException">The source is empty.</exception>
    Card Draw();

    /// <summary>True once the reshuffle mark has been reached. The engine reshuffles only between rounds.</summary>
    bool NeedsReshuffle { get; }

    /// <summary>Returns all cards to the source and shuffles them.</summary>
    void Shuffle();

    /// <summary>The number of cards that can still be drawn.</summary>
    int CardsRemaining { get; }

    /// <summary>The number of all cards in the source, dealt or not.</summary>
    int TotalCards { get; }
}
