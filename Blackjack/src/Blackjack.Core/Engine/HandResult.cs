namespace Blackjack.Core;

/// <summary>Snapshot of one settled player hand.</summary>
/// <param name="Cards">The cards of the hand.</param>
/// <param name="Total">The final total.</param>
/// <param name="IsSoft">Whether the final hand is soft.</param>
/// <param name="Bet">The bet on this hand.</param>
/// <param name="Outcome">The outcome.</param>
/// <param name="Net">Net amount won (positive) or lost (negative).</param>
public sealed record HandResult(
    IReadOnlyList<Card> Cards,
    int Total,
    bool IsSoft,
    decimal Bet,
    HandOutcome Outcome,
    decimal Net);
