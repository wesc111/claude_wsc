namespace Blackjack.Core;

/// <summary>Snapshot of a completed round.</summary>
/// <param name="TotalBet">Sum of all bets in the round.</param>
/// <param name="PlayerHands">The settled player hands.</param>
/// <param name="DealerCards">The dealer's cards.</param>
/// <param name="DealerTotal">The dealer's final total.</param>
/// <param name="Net">Net amount of the round over all hands.</param>
/// <param name="Bankroll">The bankroll after the round, or <c>null</c> if unlimited.</param>
public sealed record RoundResult(
    decimal TotalBet,
    IReadOnlyList<HandResult> PlayerHands,
    IReadOnlyList<Card> DealerCards,
    int DealerTotal,
    decimal Net,
    decimal? Bankroll);
