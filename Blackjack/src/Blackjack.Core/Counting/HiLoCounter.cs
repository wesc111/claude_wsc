namespace Blackjack.Core;

/// <summary>
/// Counts the cards seen with the Hi-Lo system (SPEC E13): 2–6 = +1, 7–9 = 0, 10–A = −1. Register it as an
/// observer of the game that deals from <c>cards</c>: it counts every face-up card and the hole card when it is
/// revealed, and starts again at 0 when the shoe is shuffled.
/// </summary>
/// <param name="cards">The shoe of the game, for the decks remaining.</param>
public sealed class HiLoCounter(ICardSource cards) : IGameObserver
{
    private const decimal CardsPerDeck = 52m;

    /// <summary>The sum of the Hi-Lo values of the cards seen since the last shuffle.</summary>
    public int RunningCount { get; private set; }

    /// <summary>The decks left in the shoe: cards remaining / 52 (the face-down hole card counts as dealt).</summary>
    public decimal DecksRemaining => cards.CardsRemaining / CardsPerDeck;

    /// <summary>The running count per deck remaining, exact (not rounded); the running count itself if the shoe is empty.</summary>
    public decimal TrueCount => cards.CardsRemaining == 0 ? RunningCount : RunningCount / DecksRemaining;

    /// <summary>The Hi-Lo value of a card.</summary>
    public static int Value(Card card) => card.BaseValue switch
    {
        <= 6 => 1,
        <= 9 => 0,
        _ => -1,
    };

    /// <summary>Starts again at 0, e.g. for a new shoe.</summary>
    public void Reset() => RunningCount = 0;

    /// <inheritdoc />
    public ValueTask OnShoeShuffledAsync(CancellationToken ct)
    {
        Reset();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnCardDealtAsync(CardDealtNotification notification, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (notification.Card is { } card)
        {
            RunningCount += Value(card);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnHoleCardRevealedAsync(HoleCardRevealedNotification notification, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(notification);
        RunningCount += Value(notification.Card);
        return ValueTask.CompletedTask;
    }
}
