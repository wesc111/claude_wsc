namespace Blackjack.Core;

/// <summary>Data of <see cref="IGameObserver.OnHandSettledAsync"/>.</summary>
/// <param name="HandIndex">Zero-based index of the hand.</param>
/// <param name="Outcome">The outcome of the hand.</param>
/// <param name="Net">Net amount won (positive) or lost (negative).</param>
public sealed record HandSettledNotification(int HandIndex, HandOutcome Outcome, decimal Net);
