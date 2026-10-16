namespace Blackjack.Core;

/// <summary>Decides whether the dealer draws, according to <see cref="Rules.DealerHitsSoft17"/>.</summary>
public sealed class DealerStrategy
{
    private readonly bool _hitsSoft17;

    /// <summary>Creates the dealer strategy for <paramref name="rules"/>.</summary>
    /// <param name="rules">The table rules.</param>
    public DealerStrategy(Rules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _hitsSoft17 = rules.DealerHitsSoft17;
    }

    /// <summary>True if the dealer must draw another card: always on 16 or less, on soft 17 only under H17.</summary>
    /// <param name="hand">The dealer's hand.</param>
    public bool ShouldHit(Hand hand)
    {
        ArgumentNullException.ThrowIfNull(hand);
        return hand.Total < 17 || (hand.Total == 17 && hand.IsSoft && _hitsSoft17);
    }
}
