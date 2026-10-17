namespace Blackjack.Core;

/// <summary>Data of <see cref="IGameObserver.OnHandDoubledAsync"/>.</summary>
/// <param name="HandIndex">Index of the doubled hand.</param>
/// <param name="NewBet">The bet of the hand after doubling.</param>
public sealed record HandDoubledNotification(int HandIndex, decimal NewBet);
