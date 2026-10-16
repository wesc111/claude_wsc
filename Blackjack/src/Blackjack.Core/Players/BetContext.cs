namespace Blackjack.Core;

/// <summary>Information available when placing a bet.</summary>
/// <param name="Bankroll">Current bankroll, or <c>null</c> if unlimited.</param>
/// <param name="Rules">The table rules, including the bet limits.</param>
public sealed record BetContext(decimal? Bankroll, Rules Rules);
