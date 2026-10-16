namespace Blackjack.Core;

/// <summary>Information available when placing a bet.</summary>
/// <param name="Bankroll">Current bankroll, or <c>null</c> if unlimited.</param>
/// <param name="Rules">The table rules, including the bet limits.</param>
public sealed record BetContext(decimal? Bankroll, Rules Rules)
{
    /// <summary>The largest valid bet: <see cref="Rules.MaxBet"/>, limited by the bankroll.</summary>
    public decimal MaxAllowedBet => Bankroll is { } available ? Math.Min(Rules.MaxBet, available) : Rules.MaxBet;

    /// <summary>
    /// True if <paramref name="bet"/> is at least <see cref="Rules.MinBet"/>, at most <see cref="Rules.MaxBet"/>
    /// and does not exceed the bankroll. The engine applies the same check.
    /// </summary>
    /// <param name="bet">The bet to check.</param>
    public bool IsValidBet(decimal bet) => bet >= Rules.MinBet && bet <= MaxAllowedBet;
}
