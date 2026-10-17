namespace Blackjack.Core;

/// <summary>Engine-internal state of one player hand: the cards plus bet and split/double flags.</summary>
internal sealed class PlayerHand(Hand hand, decimal bet)
{
    /// <summary>The cards of the hand.</summary>
    public Hand Hand { get; } = hand;

    /// <summary>The bet on this hand, including a doubled amount.</summary>
    public decimal Bet { get; set; } = bet;

    /// <summary>Whether the hand was doubled.</summary>
    public bool IsDoubled { get; set; }

    /// <summary>Whether the hand comes from splitting Aces (one card only, no Hit or Double).</summary>
    public bool IsSplitAces { get; init; }
}
