namespace Blackjack.Core;

/// <summary>Snapshot of a completed round.</summary>
/// <param name="TotalBet">Sum of all bets in the round, including the additional bets of Double and Split.</param>
/// <param name="PlayerHands">The settled player hands.</param>
/// <param name="DealerCards">The dealer's cards.</param>
/// <param name="DealerTotal">The dealer's final total.</param>
/// <param name="Net">Net amount of the round over all hands.</param>
/// <param name="Bankroll">The bankroll after the round, or <c>null</c> if unlimited.</param>
/// <param name="InitialBet">The original bet placed before any Double or Split.</param>
/// <param name="InsuranceBet">The insurance bet, or 0 if no insurance was taken; not part of <paramref name="TotalBet"/>.</param>
/// <param name="InsuranceNet">The net amount of the insurance; included in <paramref name="Net"/>.</param>
public sealed record RoundResult(
    decimal TotalBet,
    IReadOnlyList<HandResult> PlayerHands,
    IReadOnlyList<Card> DealerCards,
    int DealerTotal,
    decimal Net,
    decimal? Bankroll,
    decimal InitialBet,
    decimal InsuranceBet = 0m,
    decimal InsuranceNet = 0m);
