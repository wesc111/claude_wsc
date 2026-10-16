namespace Blackjack.Core;

/// <summary>Data of <see cref="IGameObserver.OnRoundStartedAsync"/>.</summary>
/// <param name="Bet">The bet of the round.</param>
/// <param name="Bankroll">The bankroll before the round, or <c>null</c> if unlimited.</param>
public sealed record RoundStartedNotification(decimal Bet, decimal? Bankroll);
