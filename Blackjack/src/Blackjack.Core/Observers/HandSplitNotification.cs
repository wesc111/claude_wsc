namespace Blackjack.Core;

/// <summary>Data of <see cref="IGameObserver.OnHandSplitAsync"/>.</summary>
/// <param name="HandIndex">Index of the hand that was split; it keeps the first card.</param>
/// <param name="NewHandIndex">Index of the new hand, appended at the end.</param>
/// <param name="MovedCard">The card that moved to the new hand.</param>
public sealed record HandSplitNotification(int HandIndex, int NewHandIndex, Card MovedCard);
