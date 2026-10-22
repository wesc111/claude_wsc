namespace Blackjack.Core;

/// <summary>
/// A deviation from basic strategy at a given Hi-Lo true count (SPEC E13), e.g. hard 16 vs 10: Stand at true count ≥ 0.
/// </summary>
/// <param name="Total">The hard total, or the value of each card of a pair.</param>
/// <param name="IsPair">Whether the play is for a pair (10,10) rather than a hard total.</param>
/// <param name="Upcard">The dealer upcard: 2–10, 11 = Ace.</param>
/// <param name="Basic">The basic strategy action the play replaces.</param>
/// <param name="Deviation">The action at the index.</param>
/// <param name="Index">The true count of the deviation.</param>
/// <param name="AtOrAbove"><c>true</c>: deviate at true count ≥ <paramref name="Index"/>; <c>false</c>: below it.</param>
public sealed record IndexPlay(int Total, bool IsPair, int Upcard, PlayerAction Basic, PlayerAction Deviation, decimal Index, bool AtOrAbove)
{
    /// <summary>Whether the deviation applies at <paramref name="trueCount"/>.</summary>
    public bool AppliesAt(decimal trueCount) => AtOrAbove ? trueCount >= Index : trueCount < Index;

    /// <summary>Whether the play is about this hand against this upcard.</summary>
    public bool Matches(DecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var upcard = context.DealerUpcard.BaseValue;
        if (upcard != Upcard)
        {
            return false;
        }

        var cards = context.PlayerCards;
        return IsPair
            ? cards.Count == 2 && cards[0].BaseValue == Total && cards[1].BaseValue == Total
            : !context.IsSoft && context.PlayerTotal == Total;
    }
}
